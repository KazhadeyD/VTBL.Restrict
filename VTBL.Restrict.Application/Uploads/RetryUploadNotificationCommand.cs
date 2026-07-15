using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Observability;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Application.Uploads
{
    /// <summary>
    /// Повтор уведомления RMQ без повторной выкладки файла (контракт Retry, EC-01).
    /// </summary>
    public sealed class RetryUploadNotificationCommand
    {
        private readonly IUploadBatchStore _uploadBatchStore;
        private readonly IListTypeReadStore _listTypeReadStore;
        private readonly IUploadNotifier _uploadNotifier;
        private readonly ILogger _logger;

        public RetryUploadNotificationCommand(
            IUploadBatchStore uploadBatchStore,
            IListTypeReadStore listTypeReadStore,
            IUploadNotifier uploadNotifier,
            ILogger<RetryUploadNotificationCommand> logger = null)
        {
            _uploadBatchStore = uploadBatchStore;
            _listTypeReadStore = listTypeReadStore;
            _uploadNotifier = uploadNotifier;
            _logger = logger ?? NullLogger<RetryUploadNotificationCommand>.Instance;
        }

        /// <summary>
        /// Повторяет publish по существующему UploadBatch. IFileShareStore не используется.
        /// </summary>
        public async Task<RetryUploadNotificationResult> ExecuteAsync(
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            using (OperationLogScope.BeginRetry(_logger, correlationId))
            {
                _logger.LogInformation("Retry notify started correlationId={CorrelationId}", correlationId);

                if (correlationId == Guid.Empty)
                {
                    return LogFail(correlationId, null, UploadErrorCodes.Validation, "Некорректный correlationId.");
                }

                var batch = await _uploadBatchStore.GetByCorrelationIdAsync(correlationId, cancellationToken)
                    .ConfigureAwait(false);

                if (batch == null)
                {
                    return LogFail(correlationId, null, UploadErrorCodes.NotFound, "Загрузка с указанным correlationId не найдена.");
                }

                if (batch.NotifyStatus == NotifyStatus.Published)
                {
                    return LogFail(
                        correlationId,
                        batch.ListTypeCode,
                        UploadErrorCodes.Conflict,
                        "Уведомление уже отправлено. Повторная публикация запрещена.");
                }

                if (batch.NotifyStatus != NotifyStatus.Failed && batch.NotifyStatus != NotifyStatus.Pending)
                {
                    return LogFail(
                        correlationId,
                        batch.ListTypeCode,
                        UploadErrorCodes.Validation,
                        "Статус загрузки не допускает повтор уведомления.");
                }

                var listType = await _listTypeReadStore.GetByIdAsync(batch.ListTypeId, cancellationToken)
                    .ConfigureAwait(false);

                if (listType == null)
                {
                    return LogFail(correlationId, batch.ListTypeCode, UploadErrorCodes.Db, "Тип списка для загрузки не найден.");
                }

                using (OperationLogScope.BeginRetry(_logger, correlationId, listType.Code))
                {
                    var message = new RestrictFileUploadedMessage
                    {
                        MessageType = "RestrictFileUploaded",
                        SchemaVersion = 1,
                        CorrelationId = batch.CorrelationId,
                        ListType = listType.Code,
                        FilePath = batch.StoredFilePath,
                        OriginalFileName = batch.OriginalFileName,
                        UploadedAtUtc = batch.UploadedAtUtc,
                        UploadedBy = batch.UploadedBy
                    };

                    var routingKey = "restrict.upload." + listType.RoutingKeySuffix;

                    try
                    {
                        await _uploadNotifier.PublishUploadedAsync(message, routingKey, cancellationToken)
                            .ConfigureAwait(false);

                        await _uploadBatchStore.UpdateNotifyStatusAsync(
                                correlationId,
                                NotifyStatus.Published,
                                cancellationToken)
                            .ConfigureAwait(false);

                        _logger.LogInformation(
                            "Retry notify succeeded correlationId={CorrelationId} listType={ListType}",
                            correlationId,
                            listType.Code);

                        return new RetryUploadNotificationResult
                        {
                            Success = true,
                            CorrelationId = correlationId,
                            ErrorCode = null,
                            Message = "Уведомление повторно отправлено."
                        };
                    }
                    catch (Exception)
                    {
                        try
                        {
                            await _uploadBatchStore.UpdateNotifyStatusAsync(
                                    correlationId,
                                    NotifyStatus.Failed,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // Best-effort: leave previous status if UPDATE fails.
                        }

                        return LogFail(
                            correlationId,
                            listType.Code,
                            UploadErrorCodes.Rmq,
                            "Не удалось отправить уведомление. Повторите позже.");
                    }
                }
            }
        }

        private RetryUploadNotificationResult LogFail(
            Guid correlationId,
            string listType,
            string errorCode,
            string message)
        {
            _logger.LogWarning(
                "Retry notify failed correlationId={CorrelationId} listType={ListType} errorCode={ErrorCode}",
                correlationId == Guid.Empty ? (Guid?)null : correlationId,
                listType,
                errorCode);
            return Fail(correlationId, errorCode, message);
        }

        private static RetryUploadNotificationResult Fail(Guid correlationId, string errorCode, string message)
        {
            return new RetryUploadNotificationResult
            {
                Success = false,
                CorrelationId = correlationId == Guid.Empty ? (Guid?)null : correlationId,
                ErrorCode = errorCode,
                Message = message
            };
        }
    }

    public sealed class RetryUploadNotificationResult
    {
        public bool Success { get; set; }
        public Guid? CorrelationId { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
    }
}
