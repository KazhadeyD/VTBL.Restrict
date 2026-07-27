using System;
using System.Collections.Generic;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Application.ErrorProcessing
{
    /// <summary>
    /// Результат открытия кейса обработки ошибок по caseId (без token/TTL gate).
    /// </summary>
    public sealed class ErrorProcessingCaseAccessResult
    {
        public const string ReasonNotFound = "NotFound";
        public const string ReasonUnavailable = "Unavailable";
        public const string ReasonDb = "Db";

        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }
        public ErrorProcessingViewModel View { get; private set; }

        public static ErrorProcessingCaseAccessResult Ok(ErrorProcessingViewModel view)
        {
            return new ErrorProcessingCaseAccessResult
            {
                Succeeded = true,
                View = view
            };
        }

        public static ErrorProcessingCaseAccessResult NotFound()
        {
            return new ErrorProcessingCaseAccessResult
            {
                Succeeded = false,
                FailureReason = ReasonNotFound
            };
        }

        public static ErrorProcessingCaseAccessResult Unavailable()
        {
            return new ErrorProcessingCaseAccessResult
            {
                Succeeded = false,
                FailureReason = ReasonUnavailable
            };
        }

        /// <summary>
        /// Ошибка доступа к БД при открытии кейса (обработка в Get — EP-2.3).
        /// </summary>
        public static ErrorProcessingCaseAccessResult DbError()
        {
            return new ErrorProcessingCaseAccessResult
            {
                Succeeded = false,
                FailureReason = ReasonDb
            };
        }
    }

    /// <summary>
    /// Представление кейса для оператора (только данные БД).
    /// </summary>
    public sealed class ErrorProcessingViewModel
    {
        public Guid ErrorProcessingCaseId { get; set; }
        public string ListTypeCode { get; set; }
        public string ListTypeName { get; set; }
        public ErrorProcessingStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public Guid? UploadCorrelationId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string SourceFilePath { get; set; }
        public bool IsReadOnly { get; set; }
        public IReadOnlyList<ErrorProcessingItemViewModel> Items { get; set; }
    }

    public sealed class ErrorProcessingItemViewModel
    {
        public Guid ErrorProcessingItemId { get; set; }
        public string FieldCode { get; set; }
        public int? RowNumber { get; set; }
        public string RawValue { get; set; }
        public string ParserMessage { get; set; }
        public string UserValue { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
    }
}
