using System;

namespace VTBL.Restrict.Loader.Context.Entities
{
    public sealed class ErrorProcessingItemEntity
    {
        public Guid ErrorProcessingItemId { get; set; }
        public Guid ErrorProcessingCaseId { get; set; }
        public string FieldCode { get; set; }
        public int? RowNumber { get; set; }
        public string RawValue { get; set; }
        public string ParserMessage { get; set; }
        public string UserValue { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }

        public ErrorProcessingCaseEntity Case { get; set; }
    }
}
