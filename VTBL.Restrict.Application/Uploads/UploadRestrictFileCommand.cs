using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Observability;
using VTBL.Restrict.Application.Options;
using VTBL.Restrict.Domain.Uploads;
using AppNotifyMessage = VTBL.Restrict.Application.Abstractions.RestrictFileUploadedMessage;

namespace VTBL.Restrict.Application.Uploads
{
    /// <summary>
    /// Сценарий первичной загрузки: validate → write as-is → batch → RMQ (EC-08).
    /// </summary>
    public class UploadRestrictFileCommand
    {
        private readonly IListTypeReadStore _listTypeReadStore;
        private readonly RestrictStorageOptions _storageOptions;
        private readonly IUploadPathBuilder _pathBuilder;
        private readonly IFileShareStore _fileShareStore;
        private readonly IUploadBatchStore _uploadBatchStore;
        private readonly IUploadNotifier _uploadNotifier;
        private readonly ILogger _logger;

        public UploadRestrictFileCommand(
            IListTypeReadStore listTypeReadStore,
            IOptions<RestrictStorageOptions> storageOptions,
            IUploadPathBuilder pathBuilder,
            IFileShareStore fileShareStore,
            IUploadBatchStore uploadBatchStore,
            IUploadNotifier uploadNotifier,
            ILogger<UploadRestrictFileCommand> logger = null)
        {
            _listTypeReadStore = listTypeReadStore;
            _storageOptions = storageOptions?.Value ?? new RestrictStorageOptions();
            _pathBuilder = pathBuilder;
            _fileShareStore = fileShareStore;
            _uploadBatchStore = uploadBatchStore;
            _uploadNotifier = uploadNotifier;
            _logger = logger ?? NullLogger<UploadRestrictFileCommand>.Instance;
        }

        /// <summary>
        /// Параметрless ctor для узких unit-тестов счётчика. Не использовать в DI.
        /// </summary>
        protected UploadRestrictFileCommand()
        {
            _listTypeReadStore = null;
            _storageOptions = new RestrictStorageOptions();
            _pathBuilder = null;
            _fileShareStore = null;
            _uploadBatchStore = null;
            _uploadNotifier = null;
            _logger = NullLogger<UploadRestrictFileCommand>.Instance;
        }

        /// <inheritdoc cref="ExecuteAsync"/>
        public virtual async Task<UploadRestrictFileResult> ExecuteAsync(
            UploadRestrictFileRequest request,
            CancellationToken cancellationToken)
        {
            request ??= new UploadRestrictFileRequest();

            using (OperationLogScope.BeginUpload(_logger, request.ListTypeCode))
            {
                _logger.LogInformation("Upload started for listType {ListType}", request.ListTypeCode);

                var shell = UploadShellValidator.Validate(
                    request.ListTypeCode,
                    request.OriginalFileName,
                    request.ContentLength,
                    _storageOptions.AllowedExtensions,
                    _storageOptions.MaxFileSizeBytes);

                if (!shell.IsValid)
                {
                    return LogFail(UploadErrorCodes.Validation, shell.Message, null, request.ListTypeCode);
                }

                var listType = await _listTypeReadStore.GetByCodeAsync(request.ListTypeCode, cancellationToken);
                if (listType == null || !listType.IsActive)
                {
                    return LogFail(
                        UploadErrorCodes.Validation,
                        "Тип списка не найден или неактивен.",
                        null,
                        request.ListTypeCode);
                }

                if (string.IsNullOrWhiteSpace(_storageOptions.RemoteRoot))
                {
                    return LogFail(
                        UploadErrorCodes.Share,
                        "Не настроен каталог хранения файлов.",
                        null,
                        listType.Code);
                }

                if (request.Content == null)
                {
                    return LogFail(
                        UploadErrorCodes.Validation,
                        "Поток файла отсутствует.",
                        null,
                        listType.Code);
                }

                var correlationId = Guid.NewGuid();
                var utcNow = DateTime.UtcNow;
                var targetPath = _pathBuilder.BuildTargetPath(
                    _storageOptions.RemoteRoot,
                    listType.FolderSegment,
                    utcNow,
                    correlationId,
                    request.OriginalFileName);

                using (OperationLogScope.BeginUpload(_logger, listType.Code, correlationId))
                {
                    string storedPath;
                    try
                    {
                        storedPath = await _fileShareStore.WriteAsIsAsync(
                            request.Content,
                            targetPath,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        return LogFail(
                            UploadErrorCodes.Share,
                            "Не удалось сохранить файл на диск.",
                            correlationId,
                            listType.Code);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return LogFail(
                            UploadErrorCodes.Share,
                            "Нет доступа к каталогу хранения файлов.",
                            correlationId,
                            listType.Code);
                    }

                    try
                    {
                        await _uploadBatchStore.InsertPendingAsync(
                            new UploadBatchRecord
                            {
                                CorrelationId = correlationId,
                                ListTypeId = listType.ListTypeId,
                                ListTypeCode = listType.Code,
                                OriginalFileName = request.OriginalFileName,
                                StoredFilePath = storedPath,
                                UploadedBy = request.UploadedBy,
                                UploadedAtUtc = utcNow,
                                NotifyStatus = Domain.Enums.NotifyStatus.Pending
                            },
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        return LogPartialFail(
                            UploadErrorCodes.Db,
                            "Файл сохранён, но не удалось зарегистрировать загрузку в БД.",
                            correlationId,
                            storedPath,
                            listType.Code);
                    }

                    var notifyMessage = new AppNotifyMessage
                    {
                        MessageType = "RestrictFileUploaded",
                        SchemaVersion = 1,
                        CorrelationId = correlationId,
                        ListType = listType.Code,
                        FilePath = storedPath,
                        OriginalFileName = request.OriginalFileName,
                        UploadedAtUtc = utcNow,
                        UploadedBy = request.UploadedBy
                    };

                    var routingKey = "restrict.upload." + listType.RoutingKeySuffix;

                    try
                    {
                        using (OperationLogScope.BeginPublish(_logger, correlationId, listType.Code))
                        {
                            _logger.LogInformation(
                                "Upload publish started correlationId={CorrelationId} listType={ListType}",
                                correlationId,
                                listType.Code);

                            await _uploadNotifier.PublishUploadedAsync(notifyMessage, routingKey, cancellationToken)
                                .ConfigureAwait(false);

                            await _uploadBatchStore.UpdateNotifyStatusAsync(
                                correlationId,
                                Domain.Enums.NotifyStatus.Published,
                                cancellationToken).ConfigureAwait(false);

                            _logger.LogInformation(
                                "Upload publish succeeded correlationId={CorrelationId} listType={ListType}",
                                correlationId,
                                listType.Code);
                        }
                    }
                    catch (Exception)
                    {
                        await TryMarkNotifyFailedAsync(correlationId, cancellationToken).ConfigureAwait(false);

                        _logger.LogWarning(
                            "Upload publish failed correlationId={CorrelationId} listType={ListType} errorCode={ErrorCode}",
                            correlationId,
                            listType.Code,
                            UploadErrorCodes.Rmq);

                        return PartialFail(
                            UploadErrorCodes.Rmq,
                            "Файл сохранён, но уведомление не отправлено. Повторите уведомление позже.",
                            correlationId,
                            storedPath);
                    }

                    _logger.LogInformation(
                        "Upload succeeded correlationId={CorrelationId} listType={ListType}",
                        correlationId,
                        listType.Code);

                    return new UploadRestrictFileResult
                    {
                        Success = true,
                        CorrelationId = correlationId,
                        StoredFilePath = storedPath,
                        ErrorCode = null,
                        Message = "Файл успешно загружен и передан на обработку."
                    };
                }
            }
        }

        private UploadRestrictFileResult LogFail(
            string errorCode,
            string message,
            Guid? correlationId,
            string listType)
        {
            _logger.LogWarning(
                "Upload failed listType={ListType} correlationId={CorrelationId} errorCode={ErrorCode}",
                listType,
                correlationId,
                errorCode);
            return Fail(errorCode, message);
        }

        private UploadRestrictFileResult LogPartialFail(
            string errorCode,
            string message,
            Guid correlationId,
            string storedPath,
            string listType)
        {
            _logger.LogWarning(
                "Upload partial fail listType={ListType} correlationId={CorrelationId} errorCode={ErrorCode}",
                listType,
                correlationId,
                errorCode);
            return PartialFail(errorCode, message, correlationId, storedPath);
        }

        private async Task TryMarkNotifyFailedAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            try
            {
                await _uploadBatchStore.UpdateNotifyStatusAsync(
                    correlationId,
                    Domain.Enums.NotifyStatus.Failed,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: batch row exists with Pending if UPDATE fails.
            }
        }

        private static UploadRestrictFileResult Fail(string errorCode, string message)
        {
            return new UploadRestrictFileResult
            {
                Success = false,
                CorrelationId = null,
                StoredFilePath = null,
                ErrorCode = errorCode,
                Message = message
            };
        }

        private static UploadRestrictFileResult PartialFail(
            string errorCode,
            string message,
            Guid correlationId,
            string storedPath)
        {
            return new UploadRestrictFileResult
            {
                Success = false,
                CorrelationId = correlationId,
                StoredFilePath = storedPath,
                ErrorCode = errorCode,
                Message = message
            };
        }
    }

    public sealed class UploadRestrictFileRequest
    {
        public string ListTypeCode { get; set; }
        public string OriginalFileName { get; set; }
        public long ContentLength { get; set; }
        public Stream Content { get; set; }
        public string UploadedBy { get; set; }
    }

    public sealed class UploadRestrictFileResult
    {
        public bool Success { get; set; }
        public Guid? CorrelationId { get; set; }
        public string StoredFilePath { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
    }
}
