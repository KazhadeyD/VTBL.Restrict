using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Observability;
using VTBL.Restrict.Loader.Domain.Enums;

namespace VTBL.Restrict.Loader.Application.ErrorProcessing
{
    /// <summary>
    /// Сохранение правок оператора и перевод кейса в ResolvedByUser.
    /// Без token/ExpiresAt gate: caseId + Status=Pending + обязательные UserValue.
    /// UserValue в логи не пишется.
    /// </summary>
    public sealed class ResolveErrorProcessingCommand
    {
        private readonly IErrorProcessingStore _store;
        private readonly ILogger _logger;

        public ResolveErrorProcessingCommand(
            IErrorProcessingStore store,
            ILogger<ResolveErrorProcessingCommand> logger = null)
        {
            _store = store;
            _logger = logger ?? NullLogger<ResolveErrorProcessingCommand>.Instance;
        }

        public async Task<ResolveErrorProcessingResult> ExecuteAsync(
            ResolveErrorProcessingRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                _logger.LogWarning("ErrorProcessing save failed reason=null_request errorCode={ErrorCode}",
                    ErrorProcessingErrorCodes.Validation);
                return Fail(ErrorProcessingErrorCodes.Validation, "Запрос отсутствует.");
            }

            _ = request.RawToken;
            var caseId = request.ErrorProcessingCaseId;

            using (OperationLogScope.BeginSave(_logger, caseId))
            {
                _logger.LogInformation(
                    "ErrorProcessing save started caseId={CaseId} tokenPresent={TokenPresent}",
                    caseId,
                    SensitiveLog.DescribeTokenPresence(request.RawToken));

                if (caseId == Guid.Empty)
                {
                    return LogFail(caseId, null, ErrorProcessingErrorCodes.NotFound, "Кейс не найден.");
                }

                ErrorProcessingCaseRecord record;
                try
                {
                    record = await _store.GetByIdAsync(caseId, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return LogFail(caseId, null, ErrorProcessingErrorCodes.Db, "Не удалось загрузить кейс.");
                }

                if (record == null)
                {
                    return LogFail(caseId, null, ErrorProcessingErrorCodes.NotFound, "Кейс не найден.");
                }

                if (record.Status == ErrorProcessingStatus.ResolvedByUser)
                {
                    return LogFail(
                        caseId,
                        record.ListTypeCode,
                        ErrorProcessingErrorCodes.Conflict,
                        "Кейс уже обработан. Повторное сохранение запрещено.");
                }

                if (record.Status == ErrorProcessingStatus.Expired ||
                    record.Status == ErrorProcessingStatus.Cancelled)
                {
                    return LogFail(
                        caseId,
                        record.ListTypeCode,
                        ErrorProcessingErrorCodes.Unavailable,
                        "Кейс недоступен для обработки (истёк или отменён).");
                }

                if (record.Status != ErrorProcessingStatus.Pending)
                {
                    return LogFail(
                        caseId,
                        record.ListTypeCode,
                        ErrorProcessingErrorCodes.Conflict,
                        "Статус кейса не допускает сохранение.");
                }

                var items = record.Items ?? Array.Empty<ErrorProcessingItemRecord>();
                var submitted = request.ItemUserValues ?? new Dictionary<Guid, string>();
                var validationErrors = ValidateRequired(items, submitted);
                if (validationErrors.Count > 0)
                {
                    _logger.LogWarning(
                        "ErrorProcessing save failed caseId={CaseId} listType={ListType} errorCode={ErrorCode}",
                        caseId,
                        record.ListTypeCode,
                        ErrorProcessingErrorCodes.Validation);
                    return new ResolveErrorProcessingResult
                    {
                        Success = false,
                        ErrorCode = ErrorProcessingErrorCodes.Validation,
                        Message = "Заполните обязательные поля.",
                        FieldErrors = validationErrors
                    };
                }

                var valuesToSave = BuildUserValues(items, submitted);
                var resolvedBy = string.IsNullOrWhiteSpace(request.ResolvedBy)
                    ? null
                    : request.ResolvedBy.Trim();

                using (OperationLogScope.BeginSave(_logger, caseId, record.ListTypeCode))
                {
                    try
                    {
                        var resolved = await _store.ResolveAsync(
                            caseId,
                            valuesToSave,
                            resolvedBy,
                            cancellationToken).ConfigureAwait(false);

                        if (!resolved)
                        {
                            return LogFail(
                                caseId,
                                record.ListTypeCode,
                                ErrorProcessingErrorCodes.Conflict,
                                "Кейс уже обработан или недоступен. Повторное сохранение запрещено.");
                        }
                    }
                    catch (Exception)
                    {
                        return LogFail(
                            caseId,
                            record.ListTypeCode,
                            ErrorProcessingErrorCodes.Db,
                            "Не удалось сохранить изменения. Статус кейса не изменён.");
                    }

                    _logger.LogInformation(
                        "ErrorProcessing save succeeded caseId={CaseId} listType={ListType}",
                        caseId,
                        record.ListTypeCode);

                    return new ResolveErrorProcessingResult
                    {
                        Success = true,
                        ErrorCode = null,
                        Message = "Изменения сохранены. Кейс обработан."
                    };
                }
            }
        }

        private ResolveErrorProcessingResult LogFail(
            Guid caseId,
            string listType,
            string code,
            string message)
        {
            _logger.LogWarning(
                "ErrorProcessing save failed caseId={CaseId} listType={ListType} errorCode={ErrorCode}",
                caseId,
                listType,
                code);
            return Fail(code, message);
        }

        private static IReadOnlyDictionary<Guid, string> ValidateRequired(
            IReadOnlyList<ErrorProcessingItemRecord> items,
            IReadOnlyDictionary<Guid, string> submitted)
        {
            var errors = new Dictionary<Guid, string>();
            foreach (var item in items)
            {
                if (!item.IsRequired)
                {
                    continue;
                }

                submitted.TryGetValue(item.ErrorProcessingItemId, out var value);
                if (string.IsNullOrWhiteSpace(value))
                {
                    errors[item.ErrorProcessingItemId] =
                        $"Поле «{item.FieldCode}» обязательно для заполнения.";
                }
            }

            return errors;
        }

        private static IReadOnlyDictionary<Guid, string> BuildUserValues(
            IReadOnlyList<ErrorProcessingItemRecord> items,
            IReadOnlyDictionary<Guid, string> submitted)
        {
            var result = new Dictionary<Guid, string>(items.Count);
            foreach (var item in items)
            {
                submitted.TryGetValue(item.ErrorProcessingItemId, out var value);
                result[item.ErrorProcessingItemId] = value ?? string.Empty;
            }

            return result;
        }

        private static ResolveErrorProcessingResult Fail(string code, string message)
        {
            return new ResolveErrorProcessingResult
            {
                Success = false,
                ErrorCode = code,
                Message = message
            };
        }
    }

    public sealed class ResolveErrorProcessingRequest
    {
        public Guid ErrorProcessingCaseId { get; set; }

        /// <summary>
        /// Опциональный параметр «на будущее»; не используется как gate и не логируется целиком.
        /// </summary>
        public string RawToken { get; set; }

        public IReadOnlyDictionary<Guid, string> ItemUserValues { get; set; }

        public string ResolvedBy { get; set; }
    }

    public sealed class ResolveErrorProcessingResult
    {
        public bool Success { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public IReadOnlyDictionary<Guid, string> FieldErrors { get; set; }
    }
}
