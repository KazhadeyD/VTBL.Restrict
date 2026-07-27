using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Domain.Enums;

namespace VTBL.Restrict.Application.Abstractions
{
    /// <summary>
    /// Доступ к ErrorProcessingCase / ErrorProcessingItem только через БД (EC-05). Не открывать файл на шаре.
    /// </summary>
    public interface IErrorProcessingStore
    {
        Task<ErrorProcessingCaseRecord> GetByIdAsync(Guid errorProcessingCaseId, CancellationToken cancellationToken);

        /// <summary>
        /// Список кейсов со Status = Pending (NVARCHAR, ordinal ignore case), ORDER BY CreatedAt DESC.
        /// Join ListType (Code, Name). Items не загружать. ExpiresAt не фильтровать.
        /// </summary>
        Task<IReadOnlyList<ErrorProcessingCaseSummaryRecord>> ListPendingSummariesAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Атомарно сохраняет UserValue и переводит Pending → ResolvedByUser.
        /// Не трогает RawValue / ParserMessage / AccessTokenHash. Без ExpiresAt/token gate.
        /// </summary>
        /// <returns>true, если кейс был Pending и успешно закрыт; false — не найден / не Pending.</returns>
        Task<bool> ResolveAsync(
            Guid errorProcessingCaseId,
            IReadOnlyDictionary<Guid, string> itemUserValues,
            string resolvedBy,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Краткая строка Pending-кейса для списка Error Processing (без Items).
    /// </summary>
    public sealed class ErrorProcessingCaseSummaryRecord
    {
        public Guid ErrorProcessingCaseId { get; set; }
        public string ListTypeCode { get; set; }
        public string ListTypeName { get; set; }
        public ErrorProcessingStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string SourceFilePath { get; set; }
    }

    public sealed class ErrorProcessingCaseRecord
    {
        public Guid ErrorProcessingCaseId { get; set; }
        public int ListTypeId { get; set; }
        public string ListTypeCode { get; set; }
        public string ListTypeName { get; set; }
        public byte[] AccessTokenHash { get; set; }
        public ErrorProcessingStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public Guid? UploadCorrelationId { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string SourceFilePath { get; set; }
        public IReadOnlyList<ErrorProcessingItemRecord> Items { get; set; }
    }

    public sealed class ErrorProcessingItemRecord
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
