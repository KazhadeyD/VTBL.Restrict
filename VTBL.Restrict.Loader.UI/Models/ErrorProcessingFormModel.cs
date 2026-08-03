using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VTBL.Restrict.Loader.UI.Models
{
    /// <summary>
    /// Модель формы кейса Error Processing (bind + мета для Razor).
    /// </summary>
    public sealed class ErrorProcessingFormModel
    {
        public Guid ErrorProcessingCaseId { get; set; }
        public string Token { get; set; }
        public string ListTypeCode { get; set; }
        public string ListTypeName { get; set; }
        public string Status { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string SourceFilePath { get; set; }
        public Guid? UploadCorrelationId { get; set; }
        public bool IsReadOnly { get; set; }
        public List<ErrorProcessingItemFormModel> Items { get; set; } = new List<ErrorProcessingItemFormModel>();
    }

    public sealed class ErrorProcessingItemFormModel
    {
        public Guid ErrorProcessingItemId { get; set; }
        public string FieldCode { get; set; }
        public int? RowNumber { get; set; }
        public string RawValue { get; set; }
        public string ParserMessage { get; set; }

        [Display(Name = "Значение оператора")]
        public string UserValue { get; set; }

        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
    }
}
