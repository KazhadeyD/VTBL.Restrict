using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Observability;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Domain.Uploads;
using AppNotifyMessage = VTBL.Restrict.Loader.Application.Abstractions.RestrictFileUploadedMessage;

namespace VTBL.Restrict.Loader.Application.Uploads
{
    /// <summary>
    /// Сценарий загрузки: проверка, запись файла, уведомление. В БД ничего не пишем.
    /// </summary>
    public class UploadRestrictFileCommand
    {
        private readonly IListTypeReadStore _listTypeReadStore;
        private readonly RestrictStorageOptions _storageOptions;
        private readonly IUploadPathBuilder _pathBuilder;
        private readonly IFileShareStore _fileShareStore;
        private readonly IUploadNotifier _uploadNotifier;
        private readonly ILogger _logger;

        public UploadRestrictFileCommand(
            IListTypeReadStore listTypeReadStore,
            IOptions<RestrictStorageOptions> storageOptions,
            IUploadPathBuilder pathBuilder,
            IFileShareStore fileShareStore,
            IUploadNotifier uploadNotifier,
            ILogger<UploadRestrictFileCommand> logger = null)
        {
            _listTypeReadStore = listTypeReadStore;
            _storageOptions = storageOptions?.Value ?? new RestrictStorageOptions();
            _pathBuilder = pathBuilder;
            _fileShareStore = fileShareStore;
            _uploadNotifier = uploadNotifier;
            _logger = logger ?? NullLogger<UploadRestrictFileCommand>.Instance;
        }

        /// <summary>
        /// Конструктор без зависимостей для узких unit-тестов счётчика. Не использовать в DI.
        /// </summary>
        protected UploadRestrictFileCommand()
        {
            _listTypeReadStore = null;
            _storageOptions = new RestrictStorageOptions();
            _pathBuilder = null;
            _fileShareStore = null;
            _uploadNotifier = null;
            _logger = NullLogger<UploadRestrictFileCommand>.Instance;
        }

        /// <summary>
        /// Выполняет весь “боевой” пайплайн загрузки:
        /// проверка входных данных → поиск ListType → запись файла в шеру → уведомление через IUploadNotifier.
        /// </summary>
        public virtual async Task<UploadRestrictFileResult> ExecuteAsync(
            UploadRestrictFileRequest request,
            CancellationToken cancellationToken)
        {
            request ??= new UploadRestrictFileRequest();

            using (OperationLogScope.BeginUpload(_logger, request.ListTypeCode))
            {
                _logger.LogInformation("Upload started");

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
                if (listType == null)
                {
                    return LogFail(
                        UploadErrorCodes.Validation,
                        "Тип списка не найден.",
                        null,
                        request.ListTypeCode);
                }

                if (string.IsNullOrWhiteSpace(listType.RemoteRoot))
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

                // correlationId нужен сразу для двух вещей:
                // 1) чтобы файл можно было однозначно найти/сопоставить,
                // 2) чтобы потом связать логи и сообщение в Rabbit.
                var correlationId = Guid.NewGuid();
                var targetPath = _pathBuilder.BuildTargetPath(
                    listType.RemoteRoot,
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
                    catch (IOException ex)
                    {
                        return LogFail(
                            UploadErrorCodes.Share,
                            "Не удалось сохранить файл на диск.",
                            correlationId,
                            listType.Code,
                            ex);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        return LogFail(
                            UploadErrorCodes.Share,
                            "Нет доступа к каталогу хранения файлов.",
                            correlationId,
                            listType.Code,
                            ex);
                    }

                    var notifyMessage = new AppNotifyMessage
                    {
                        MessageType = "RestrictFileUploaded",
                        SchemaVersion = 1,
                        CorrelationId = correlationId,
                        ListType = listType.Code,
                        FilePath = storedPath,
                        OriginalFileName = request.OriginalFileName,
                        UploadedAtUtc = DateTime.UtcNow,
                        UploadedBy = request.UploadedBy ?? request.UserName,
                        UserId = request.UserId,
                        UserName = request.UserName ?? request.UploadedBy
                    };

                    // После успешной записи файла в “шару” уведомляем остальной пайплайн.
                    // Если Rabbit упадёт — файл уже на месте, значит это будет partial failure.
                    var routingKey = "restrict.upload." + listType.Code?.Trim().ToLowerInvariant();

                    try
                    {
                        using (OperationLogScope.BeginPublish(_logger, correlationId, listType.Code))
                        {
                            _logger.LogInformation("Upload publish started");

                            await _uploadNotifier.PublishUploadedAsync(notifyMessage, routingKey, cancellationToken)
                                .ConfigureAwait(false);

                            _logger.LogInformation("Upload publish succeeded");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Upload publish failed with {ErrorCode}", UploadErrorCodes.Rmq);

                        // Файл физически сохранён, но уведомление не дошло.
                        // Смысл — вернуть storedPath и correlationId, чтобы можно было быстро найти проблему.
                        return PartialFail(
                            UploadErrorCodes.Rmq,
                            "Файл сохранён, но уведомление не отправлено. Обратитесь в поддержку с correlationId.",
                            correlationId,
                            storedPath);
                    }

                    _logger.LogInformation("Upload succeeded");

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

        /// <summary>
        /// Унифицированный способ “провалить загрузку” с логом.
        /// Если исключение передали — добавим его в warning, чтобы потом не гадать, что пошло не так.
        /// </summary>
        private UploadRestrictFileResult LogFail(
            string errorCode,
            string message,
            Guid? correlationId,
            string listType,
            Exception exception = null)
        {
            using (OperationLogScope.BeginUpload(_logger, listType, correlationId))
            {
                if (exception != null)
                {
                    _logger.LogWarning(exception, "Upload failed with {ErrorCode}", errorCode);
                }
                else
                {
                    _logger.LogWarning("Upload failed with {ErrorCode}", errorCode);
                }
            }

            return Fail(errorCode, message);
        }

        /// <summary>
        /// Полный fail: ничего полезного оператору уже не показать (correlationId/путь не возвращаем).
        /// </summary>
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

        /// <summary>
        /// Partial fail: файл физически сохранён, но уведомление (Rabbit) не дошло.
        /// Возвращаем storedPath и correlationId, чтобы поддержка могла быстро найти конкретный артефакт.
        /// </summary>
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
}
