using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Context.Entities;
using VTBL.Restrict.Loader.Domain.Enums;

namespace VTBL.Restrict.Loader.Context.Stores
{
    public sealed class EfUploadBatchStore : IUploadBatchStore
    {
        private readonly RestrictDbContext _db;

        public EfUploadBatchStore(RestrictDbContext db)
        {
            _db = db;
        }

        public async Task InsertPendingAsync(UploadBatchRecord batch, CancellationToken cancellationToken)
        {
            var entity = new UploadBatchEntity
            {
                UploadBatchId = batch.UploadBatchId == Guid.Empty ? Guid.NewGuid() : batch.UploadBatchId,
                CorrelationId = batch.CorrelationId,
                ListTypeId = batch.ListTypeId,
                OriginalFileName = batch.OriginalFileName,
                StoredFilePath = batch.StoredFilePath,
                UploadedBy = batch.UploadedBy,
                UploadedAt = batch.UploadedAtUtc,
                NotifyStatus = "Pending"
            };

            _db.UploadBatches.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateNotifyStatusAsync(Guid correlationId, NotifyStatus status, CancellationToken cancellationToken)
        {
            var entity = await _db.UploadBatches
                .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);

            if (entity == null)
            {
                return;
            }

            entity.NotifyStatus = ToDbValue(status);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<UploadBatchRecord> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            var entity = await _db.UploadBatches
                .AsNoTracking()
                .Include(x => x.ListType)
                .FirstOrDefaultAsync(x => x.CorrelationId == correlationId, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            return new UploadBatchRecord
            {
                UploadBatchId = entity.UploadBatchId,
                CorrelationId = entity.CorrelationId,
                ListTypeId = entity.ListTypeId,
                ListTypeCode = entity.ListType?.Code,
                OriginalFileName = entity.OriginalFileName,
                StoredFilePath = entity.StoredFilePath,
                UploadedBy = entity.UploadedBy,
                UploadedAtUtc = entity.UploadedAt,
                NotifyStatus = ParseStatus(entity.NotifyStatus)
            };
        }

        private static string ToDbValue(NotifyStatus status)
        {
            switch (status)
            {
                case NotifyStatus.Published:
                    return "Published";
                case NotifyStatus.Failed:
                    return "Failed";
                default:
                    return "Pending";
            }
        }

        private static NotifyStatus ParseStatus(string value)
        {
            if (string.Equals(value, "Published", StringComparison.OrdinalIgnoreCase))
            {
                return NotifyStatus.Published;
            }

            if (string.Equals(value, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                return NotifyStatus.Failed;
            }

            return NotifyStatus.Pending;
        }
    }
}
