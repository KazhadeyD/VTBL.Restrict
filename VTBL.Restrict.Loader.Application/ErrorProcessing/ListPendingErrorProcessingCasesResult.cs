using System;
using System.Collections.Generic;
using VTBL.Restrict.Loader.Domain.Enums;

namespace VTBL.Restrict.Loader.Application.ErrorProcessing
{
    /// <summary>
    /// Результат списка Pending-кейсов Error Processing (UC-EP-01).
    /// </summary>
    public sealed class ListPendingErrorProcessingCasesResult
    {
        public bool Succeeded { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCode { get; set; }
        public IReadOnlyList<ErrorProcessingCaseSummaryDto> Items { get; set; }
    }

    /// <summary>
    /// Summary-строка кейса для UI списка Error Processing.
    /// </summary>
    public sealed class ErrorProcessingCaseSummaryDto
    {
        public Guid ErrorProcessingCaseId { get; set; }
        public string ListTypeCode { get; set; }
        public string ListTypeName { get; set; }
        public ErrorProcessingStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string SourceFilePath { get; set; }
    }
}
