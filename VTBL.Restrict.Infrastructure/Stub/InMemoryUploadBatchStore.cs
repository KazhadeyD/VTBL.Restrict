using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Infrastructure.Stub
{
    /// <summary>
    /// In-memory UploadBatch для dev/test без SQL.
    /// </summary>
    public sealed class InMemoryUploadBatchStore : IUploadBatchStore
    {
        private readonly ConcurrentDictionary<Guid, UploadBatchRecord> _byCorrelation = new ConcurrentDictionary<Guid, UploadBatchRecord>();
        private Guid _latestCorrelationId;

        public Task InsertPendingAsync(UploadBatchRecord batch, CancellationToken cancellationToken)
        {
            var copy = Clone(batch);
            copy.NotifyStatus = NotifyStatus.Pending;
            if (copy.UploadBatchId == Guid.Empty)
            {
                copy.UploadBatchId = Guid.NewGuid();
            }

            if (!_byCorrelation.TryAdd(copy.CorrelationId, copy))
            {
                throw new InvalidOperationException("UploadBatch with the same CorrelationId already exists.");
            }

            _latestCorrelationId = copy.CorrelationId;
            return Task.CompletedTask;
        }

        public Task UpdateNotifyStatusAsync(Guid correlationId, NotifyStatus status, CancellationToken cancellationToken)
        {
            if (_byCorrelation.TryGetValue(correlationId, out var existing))
            {
                existing.NotifyStatus = status;
            }

            return Task.CompletedTask;
        }

        public Task<UploadBatchRecord> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            _byCorrelation.TryGetValue(correlationId, out var found);
            return Task.FromResult(found == null ? null : Clone(found));
        }

        /// <summary>
        /// Test-only: последняя вставленная запись.
        /// </summary>
        public UploadBatchRecord GetLatestForTest()
        {
            if (_latestCorrelationId == Guid.Empty)
            {
                return null;
            }

            _byCorrelation.TryGetValue(_latestCorrelationId, out var found);
            return found == null ? null : Clone(found);
        }

        private static UploadBatchRecord Clone(UploadBatchRecord source)
        {
            return new UploadBatchRecord
            {
                UploadBatchId = source.UploadBatchId,
                CorrelationId = source.CorrelationId,
                ListTypeId = source.ListTypeId,
                ListTypeCode = source.ListTypeCode,
                OriginalFileName = source.OriginalFileName,
                StoredFilePath = source.StoredFilePath,
                UploadedBy = source.UploadedBy,
                UploadedAtUtc = source.UploadedAtUtc,
                NotifyStatus = source.NotifyStatus
            };
        }
    }
}
