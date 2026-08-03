using System;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.Enums;

namespace VTBL.Restrict.Loader.Infrastructure.Stub
{
    /// <summary>
    /// Заглушка хранилища UploadBatch (без SQL).
    /// </summary>
    public sealed class StubUploadBatchStore : IUploadBatchStore
    {
        public Task InsertPendingAsync(UploadBatchRecord batch, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpdateNotifyStatusAsync(Guid correlationId, NotifyStatus status, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<UploadBatchRecord> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            return Task.FromResult<UploadBatchRecord>(null);
        }
    }
}
