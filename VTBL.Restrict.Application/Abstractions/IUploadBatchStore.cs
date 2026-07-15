using System;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Application.Abstractions
{
    /// <summary>
    /// Учёт загрузки (UploadBatch) и статус уведомления RMQ.
    /// </summary>
    public interface IUploadBatchStore
    {
        Task InsertPendingAsync(UploadBatchRecord batch, CancellationToken cancellationToken);
        Task UpdateNotifyStatusAsync(Guid correlationId, NotifyStatus status, CancellationToken cancellationToken);
        Task<UploadBatchRecord> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Запись UploadBatch для портов Application.
    /// </summary>
    public sealed class UploadBatchRecord
    {
        public Guid UploadBatchId { get; set; }
        public Guid CorrelationId { get; set; }
        public int ListTypeId { get; set; }
        public string ListTypeCode { get; set; }
        public string OriginalFileName { get; set; }
        public string StoredFilePath { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedAtUtc { get; set; }
        public NotifyStatus NotifyStatus { get; set; }
    }
}
