using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Context.Stores
{
    public sealed class EfErrorProcessingCaseStore : IErrorProcessingStore
    {
        private readonly RestrictDbContext _db;

        public EfErrorProcessingCaseStore(RestrictDbContext db)
        {
            _db = db;
        }

        public async Task<ErrorProcessingCaseRecord> GetByIdAsync(
            Guid errorProcessingCaseId,
            CancellationToken cancellationToken)
        {
            var entity = await _db.ErrorProcessingCases
                .AsNoTracking()
                .Include(x => x.ListType)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.ErrorProcessingCaseId == errorProcessingCaseId, cancellationToken);

            if (entity == null)
            {
                return null;
            }

            var items = (entity.Items ?? Enumerable.Empty<Entities.ErrorProcessingItemEntity>())
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.FieldCode)
                .Select(i => new ErrorProcessingItemRecord
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

            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = entity.ErrorProcessingCaseId,
                ListTypeId = entity.ListTypeId,
                ListTypeCode = entity.ListType?.Code,
                ListTypeName = entity.ListType?.Name,
                AccessTokenHash = entity.AccessTokenHash,
                Status = ParseStatus(entity.Status),
                ExpiresAtUtc = entity.ExpiresAt,
                SourceFilePath = entity.SourceFilePath,
                Items = items
            };
        }

        public async Task<bool> ResolveAsync(
            Guid errorProcessingCaseId,
            IReadOnlyDictionary<Guid, string> itemUserValues,
            string resolvedBy,
            CancellationToken cancellationToken)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var entity = await _db.ErrorProcessingCases
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.ErrorProcessingCaseId == errorProcessingCaseId, cancellationToken);

                if (entity == null || !string.Equals(entity.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                {
                    tx.Rollback();
                    return false;
                }

                if (itemUserValues != null && entity.Items != null)
                {
                    foreach (var item in entity.Items)
                    {
                        if (itemUserValues.TryGetValue(item.ErrorProcessingItemId, out var userValue))
                        {
                            item.UserValue = userValue;
                        }
                    }
                }

                entity.Status = "ResolvedByUser";
                entity.ResolvedAt = DateTime.UtcNow;
                entity.ResolvedBy = resolvedBy;

                await _db.SaveChangesAsync(cancellationToken);
                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static ErrorProcessingStatus ParseStatus(string value)
        {
            if (string.Equals(value, "ResolvedByUser", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorProcessingStatus.ResolvedByUser;
            }

            if (string.Equals(value, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorProcessingStatus.Expired;
            }

            if (string.Equals(value, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return ErrorProcessingStatus.Cancelled;
            }

            return ErrorProcessingStatus.Pending;
        }
    }
}
