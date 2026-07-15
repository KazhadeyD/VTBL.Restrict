using System;
using System.Collections.Generic;

namespace VTBL.Restrict.Context.Entities
{
    public sealed class ErrorProcessingCaseEntity
    {
        public Guid ErrorProcessingCaseId { get; set; }
        public int ListTypeId { get; set; }
        public Guid? UploadCorrelationId { get; set; }
        public byte[] AccessTokenHash { get; set; }
        public string Status { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string SourceFilePath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; }

        public ListTypeEntity ListType { get; set; }
        public ICollection<ErrorProcessingItemEntity> Items { get; set; }
    }
}
