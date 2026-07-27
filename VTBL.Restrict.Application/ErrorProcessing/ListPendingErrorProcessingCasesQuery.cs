using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Observability;

namespace VTBL.Restrict.Application.ErrorProcessing
{
    /// <summary>
    /// Список Pending-кейсов Error Processing для оператора (UC-EP-01).
    /// </summary>
    public sealed class ListPendingErrorProcessingCasesQuery
    {
        private readonly IErrorProcessingStore _store;
        private readonly ILogger _logger;

        public ListPendingErrorProcessingCasesQuery(
            IErrorProcessingStore store,
            ILogger<ListPendingErrorProcessingCasesQuery> logger = null)
        {
            _store = store;
            _logger = logger ?? NullLogger<ListPendingErrorProcessingCasesQuery>.Instance;
        }

        /// <summary>
        /// Возвращает список Pending summary из store; при Db-failure — Failed с кодом Db.
        /// Файл и Upload-порты не используются.
        /// </summary>
        public async Task<ListPendingErrorProcessingCasesResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            using (OperationLogScope.BeginList(_logger))
            {
                _logger.LogInformation("ErrorProcessing list started");

                try
                {
                    var records = await _store.ListPendingSummariesAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var items = (records ?? Array.Empty<ErrorProcessingCaseSummaryRecord>())
                        .Select(MapSummary)
                        .ToList();

                    _logger.LogInformation(
                        "ErrorProcessing list succeeded count={Count}",
                        items.Count);

                    return new ListPendingErrorProcessingCasesResult
                    {
                        Succeeded = true,
                        Items = items
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "ErrorProcessing list failed errorCode={ErrorCode}",
                        ErrorProcessingErrorCodes.Db);
                    _logger.LogWarning(
                        "ErrorProcessing list failed errorCode={ErrorCode}",
                        ErrorProcessingErrorCodes.Db);

                    return new ListPendingErrorProcessingCasesResult
                    {
                        Succeeded = false,
                        ErrorCode = ErrorProcessingErrorCodes.Db,
                        ErrorMessage = "Не удалось загрузить список кейсов.",
                        Items = Array.Empty<ErrorProcessingCaseSummaryDto>()
                    };
                }
            }
        }

        private static ErrorProcessingCaseSummaryDto MapSummary(ErrorProcessingCaseSummaryRecord record)
        {
            return new ErrorProcessingCaseSummaryDto
            {
                ErrorProcessingCaseId = record.ErrorProcessingCaseId,
                ListTypeCode = record.ListTypeCode,
                ListTypeName = record.ListTypeName,
                Status = record.Status,
                CreatedAtUtc = record.CreatedAtUtc,
                ExpiresAtUtc = record.ExpiresAtUtc,
                SourceFilePath = record.SourceFilePath
            };
        }
    }
}
