using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.Enums;

namespace VTBL.Restrict.Loader.Infrastructure.Stub
{
    /// <summary>
    /// In-memory ErrorProcessing store для тестов/dev без SQL (EC-05: только данные в памяти, без файла).
    /// Resolve атомарно: либо полная замена записи, либо без изменений.
    /// </summary>
    public sealed class InMemoryErrorProcessingCaseStore : IErrorProcessingStore
    {
        private readonly ConcurrentDictionary<Guid, ErrorProcessingCaseRecord> _cases =
            new ConcurrentDictionary<Guid, ErrorProcessingCaseRecord>();

        private readonly object _resolveGate = new object();

        /// <summary>
        /// Если задан — вызывается после подготовки черновика и до commit; исключение имитирует mid-tx failure (rollback).
        /// </summary>
        public Action MidResolveFault { get; set; }

        public void Seed(ErrorProcessingCaseRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            _cases[record.ErrorProcessingCaseId] = Clone(record);
        }

        public Task<ErrorProcessingCaseRecord> GetByIdAsync(
            Guid errorProcessingCaseId,
            CancellationToken cancellationToken)
        {
            _cases.TryGetValue(errorProcessingCaseId, out var found);
            return Task.FromResult(found == null ? null : Clone(found));
        }

        /// <summary>
        /// Pending summary: фильтр Status=Pending (ignore case), ORDER BY CreatedAtUtc DESC;
        /// без Items; ExpiresAt не фильтруется.
        /// </summary>
        public Task<IReadOnlyList<ErrorProcessingCaseSummaryRecord>> ListPendingSummariesAsync(
            CancellationToken cancellationToken)
        {
            _ = cancellationToken;

            IReadOnlyList<ErrorProcessingCaseSummaryRecord> items = _cases.Values
                .Where(c => string.Equals(
                    c.Status.ToString(),
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.CreatedAtUtc)
                .Select(c => new ErrorProcessingCaseSummaryRecord
                {
                    ErrorProcessingCaseId = c.ErrorProcessingCaseId,
                    ListTypeCode = c.ListTypeCode,
                    ListTypeName = c.ListTypeName,
                    Status = c.Status,
                    CreatedAtUtc = c.CreatedAtUtc,
                    ExpiresAtUtc = c.ExpiresAtUtc,
                    SourceFilePath = c.SourceFilePath
                })
                .ToList();

            return Task.FromResult(items);
        }

        public Task<bool> ResolveAsync(
            Guid errorProcessingCaseId,
            IReadOnlyDictionary<Guid, string> itemUserValues,
            string resolvedBy,
            CancellationToken cancellationToken)
        {
            lock (_resolveGate)
            {
                if (!_cases.TryGetValue(errorProcessingCaseId, out var existing))
                {
                    return Task.FromResult(false);
                }

                if (existing.Status != ErrorProcessingStatus.Pending)
                {
                    return Task.FromResult(false);
                }

                var draft = Clone(existing);
                var items = draft.Items?.Select(CloneItem).ToList() ?? new List<ErrorProcessingItemRecord>();
                if (itemUserValues != null)
                {
                    foreach (var item in items)
                    {
                        if (itemUserValues.TryGetValue(item.ErrorProcessingItemId, out var value))
                        {
                            item.UserValue = value;
                        }
                    }
                }

                draft.Items = items;
                draft.Status = ErrorProcessingStatus.ResolvedByUser;

                MidResolveFault?.Invoke();

                _cases[errorProcessingCaseId] = draft;
                return Task.FromResult(true);
            }
        }

        private static ErrorProcessingCaseRecord Clone(ErrorProcessingCaseRecord source)
        {
            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = source.ErrorProcessingCaseId,
                ListTypeId = source.ListTypeId,
                ListTypeCode = source.ListTypeCode,
                ListTypeName = source.ListTypeName,
                AccessTokenHash = source.AccessTokenHash == null
                    ? null
                    : (byte[])source.AccessTokenHash.Clone(),
                Status = source.Status,
                CreatedAtUtc = source.CreatedAtUtc,
                UploadCorrelationId = source.UploadCorrelationId,
                ExpiresAtUtc = source.ExpiresAtUtc,
                SourceFilePath = source.SourceFilePath,
                Items = source.Items?.Select(CloneItem).ToList() ?? new List<ErrorProcessingItemRecord>()
            };
        }

        private static ErrorProcessingItemRecord CloneItem(ErrorProcessingItemRecord source)
        {
            return new ErrorProcessingItemRecord
            {
                ErrorProcessingItemId = source.ErrorProcessingItemId,
                FieldCode = source.FieldCode,
                RowNumber = source.RowNumber,
                RawValue = source.RawValue,
                ParserMessage = source.ParserMessage,
                UserValue = source.UserValue,
                IsRequired = source.IsRequired,
                SortOrder = source.SortOrder
            };
        }
    }
}
