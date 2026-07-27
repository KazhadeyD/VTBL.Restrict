using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Observability;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Application.ErrorProcessing
{
    /// <summary>
    /// Открытие обработки ошибок по caseId (только БД, EC-05).
    /// Token игнорируется; ExpiresAt / AccessTokenHash не используются как gate.
    /// </summary>
    public sealed class GetErrorProcessingForOperatorQuery
    {
        private readonly IErrorProcessingStore _store;
        private readonly ILogger _logger;

        public GetErrorProcessingForOperatorQuery(
            IErrorProcessingStore store,
            ILogger<GetErrorProcessingForOperatorQuery> logger = null)
        {
            _store = store;
            _logger = logger ?? NullLogger<GetErrorProcessingForOperatorQuery>.Instance;
        }

        /// <summary>
        /// Загружает кейс по caseId. Параметр <paramref name="rawToken"/> игнорируется и не пишется в лог.
        /// </summary>
        public async Task<ErrorProcessingCaseAccessResult> ExecuteAsync(
            Guid caseId,
            string rawToken,
            CancellationToken cancellationToken)
        {
            using (OperationLogScope.BeginOpen(_logger, caseId))
            {
                _logger.LogInformation(
                    "ErrorProcessing open started caseId={CaseId} tokenPresent={TokenPresent}",
                    caseId,
                    SensitiveLog.DescribeTokenPresence(rawToken));

                if (caseId == Guid.Empty)
                {
                    _logger.LogWarning(
                        "ErrorProcessing open failed caseId={CaseId} reason={Reason} tokenPresent={TokenPresent}",
                        caseId,
                        ErrorProcessingCaseAccessResult.ReasonNotFound,
                        SensitiveLog.DescribeTokenPresence(rawToken));
                    return ErrorProcessingCaseAccessResult.NotFound();
                }

                try
                {
                    var record = await _store.GetByIdAsync(caseId, cancellationToken).ConfigureAwait(false);
                    if (record == null)
                    {
                        _logger.LogWarning(
                            "ErrorProcessing open failed caseId={CaseId} reason={Reason} tokenPresent={TokenPresent}",
                            caseId,
                            ErrorProcessingCaseAccessResult.ReasonNotFound,
                            SensitiveLog.DescribeTokenPresence(rawToken));
                        return ErrorProcessingCaseAccessResult.NotFound();
                    }

                    if (record.Status == ErrorProcessingStatus.Expired ||
                        record.Status == ErrorProcessingStatus.Cancelled)
                    {
                        _logger.LogWarning(
                            "ErrorProcessing open failed caseId={CaseId} listType={ListType} reason={Reason} tokenPresent={TokenPresent}",
                            caseId,
                            record.ListTypeCode,
                            ErrorProcessingCaseAccessResult.ReasonUnavailable,
                            SensitiveLog.DescribeTokenPresence(rawToken));
                        return ErrorProcessingCaseAccessResult.Unavailable();
                    }

                    var isReadOnly = record.Status == ErrorProcessingStatus.ResolvedByUser;
                    var view = MapView(record, isReadOnly);

                    using (OperationLogScope.BeginOpen(_logger, caseId, record.ListTypeCode))
                    {
                        _logger.LogInformation(
                            "ErrorProcessing open succeeded caseId={CaseId} listType={ListType} status={Status} readOnly={ReadOnly} tokenPresent={TokenPresent}",
                            caseId,
                            record.ListTypeCode,
                            record.Status,
                            isReadOnly,
                            SensitiveLog.DescribeTokenPresence(rawToken));
                    }

                    return ErrorProcessingCaseAccessResult.Ok(view);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "ErrorProcessing open failed caseId={CaseId} reason={Reason} errorCode={ErrorCode} tokenPresent={TokenPresent}",
                        caseId,
                        ErrorProcessingCaseAccessResult.ReasonDb,
                        ErrorProcessingErrorCodes.Db,
                        SensitiveLog.DescribeTokenPresence(rawToken));

                    return ErrorProcessingCaseAccessResult.DbError();
                }
            }
        }

        private static ErrorProcessingViewModel MapView(ErrorProcessingCaseRecord record, bool isReadOnly)
        {
            var items = (record.Items ?? Array.Empty<ErrorProcessingItemRecord>())
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.FieldCode)
                .Select(i => new ErrorProcessingItemViewModel
                {
                    ErrorProcessingItemId = i.ErrorProcessingItemId,
                    FieldCode = i.FieldCode,
                    RowNumber = i.RowNumber,
                    RawValue = i.RawValue,
                    ParserMessage = i.ParserMessage,
                    UserValue = i.UserValue,
                    IsRequired = i.IsRequired,
                    SortOrder = i.SortOrder
                })
                .ToList();

            return new ErrorProcessingViewModel
            {
                ErrorProcessingCaseId = record.ErrorProcessingCaseId,
                ListTypeCode = record.ListTypeCode,
                ListTypeName = record.ListTypeName,
                Status = record.Status,
                CreatedAtUtc = record.CreatedAtUtc,
                UploadCorrelationId = record.UploadCorrelationId,
                ExpiresAtUtc = record.ExpiresAtUtc,
                SourceFilePath = record.SourceFilePath,
                IsReadOnly = isReadOnly,
                Items = items
            };
        }
    }
}
