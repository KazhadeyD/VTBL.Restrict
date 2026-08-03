using System;

namespace VTBL.Restrict.Loader.Context.Entities
{
    public sealed class UploadBatchEntity
    {
        public Guid UploadBatchId { get; set; }
        public Guid CorrelationId { get; set; }
        public int ListTypeId { get; set; }
        public string OriginalFileName { get; set; }
        public string StoredFilePath { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public string NotifyStatus { get; set; }

        public ListTypeEntity ListType { get; set; }
    }
}
