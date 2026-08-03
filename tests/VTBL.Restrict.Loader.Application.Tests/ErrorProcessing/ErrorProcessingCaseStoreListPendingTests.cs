using System;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Loader.Application.Tests.ErrorProcessing
{
    /// <summary>
    /// Store ListPending + GetById enrich (InMemory; live SQL — EP-4.1).
    /// </summary>
    public sealed class ErrorProcessingCaseStoreListPendingTests
    {
        /// <summary>
        /// TC-E2E-01: 2 Pending + 1 Resolved → только 2 Pending, CreatedAt DESC.
        /// </summary>
        [Fact]
        public async Task ListPending_TwoPendingOneResolved_ReturnsOnlyPendingOrderedByCreatedAtDesc()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var olderPending = Guid.NewGuid();
            var newerPending = Guid.NewGuid();
            var resolved = Guid.NewGuid();

            store.Seed(CreateCase(
                olderPending,
                ErrorProcessingStatus.Pending,
                createdAtUtc: new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
            store.Seed(CreateCase(
                newerPending,
                ErrorProcessingStatus.Pending,
                createdAtUtc: new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)));
            store.Seed(CreateCase(
                resolved,
                ErrorProcessingStatus.ResolvedByUser,
                createdAtUtc: new DateTime(2026, 7, 15, 18, 0, 0, DateTimeKind.Utc)));

            var items = await store.ListPendingSummariesAsync(CancellationToken.None);

            Assert.Equal(2, items.Count);
            Assert.Equal(newerPending, items[0].ErrorProcessingCaseId);
            Assert.Equal(olderPending, items[1].ErrorProcessingCaseId);
            Assert.DoesNotContain(items, x => x.ErrorProcessingCaseId == resolved);
            Assert.All(items, x => Assert.Equal(ErrorProcessingStatus.Pending, x.Status));
        }

        /// <summary>
        /// TC-E2E-02: Pending с ExpiresAt в прошлом остаётся в list.
        /// </summary>
        [Fact]
        public async Task ListPending_PendingWithPastExpiresAt_RemainsInList()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var caseId = Guid.NewGuid();
            var pastExpires = DateTime.UtcNow.AddDays(-3);

            store.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-5),
                ExpiresAtUtc = pastExpires,
                SourceFilePath = @"\\share\inbox\mvk\expired.xlsx",
                Items = Array.Empty<ErrorProcessingItemRecord>()
            });

            var items = await store.ListPendingSummariesAsync(CancellationToken.None);

            Assert.Single(items);
            Assert.Equal(caseId, items[0].ErrorProcessingCaseId);
            Assert.True(items[0].ExpiresAtUtc < DateTime.UtcNow);
            Assert.Equal(ErrorProcessingStatus.Pending, items[0].Status);
        }

        /// <summary>
        /// TC-UNIT-01: ListPending не возвращает / не требует Items.
        /// </summary>
        [Fact]
        public async Task ListPending_DoesNotRequireOrExposeItems()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var withItems = Guid.NewGuid();
            var withoutItems = Guid.NewGuid();

            store.Seed(CreateCase(
                withItems,
                ErrorProcessingStatus.Pending,
                createdAtUtc: DateTime.UtcNow.AddHours(-2),
                items: new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = Guid.NewGuid(),
                        FieldCode = "F1",
                        IsRequired = true,
                        SortOrder = 1
                    }
                }));
            store.Seed(CreateCase(
                withoutItems,
                ErrorProcessingStatus.Pending,
                createdAtUtc: DateTime.UtcNow.AddHours(-1),
                items: null));

            var items = await store.ListPendingSummariesAsync(CancellationToken.None);

            Assert.Equal(2, items.Count);
            Assert.All(items, x =>
            {
                Assert.False(string.IsNullOrEmpty(x.ListTypeCode));
                Assert.Equal(ErrorProcessingStatus.Pending, x.Status);
            });
            Assert.Contains(items, x => x.ErrorProcessingCaseId == withItems);
            Assert.Contains(items, x => x.ErrorProcessingCaseId == withoutItems);
            // SummaryRecord не содержит Items — контракт списка без загрузки items
            Assert.Null(typeof(ErrorProcessingCaseSummaryRecord).GetProperty("Items"));
        }

        /// <summary>
        /// TC-UNIT-02: GetById после Seed пробрасывает CreatedAtUtc и UploadCorrelationId.
        /// </summary>
        [Fact]
        public async Task GetById_AfterSeed_PropagatesCreatedAtAndUploadCorrelationId()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var caseId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var createdAt = new DateTime(2026, 7, 15, 9, 30, 0, DateTimeKind.Utc);

            store.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = createdAt,
                UploadCorrelationId = correlationId,
                ExpiresAtUtc = createdAt.AddDays(2),
                SourceFilePath = @"\\share\inbox\mvk\file.xlsx",
                Items = Array.Empty<ErrorProcessingItemRecord>()
            });

            var record = await store.GetByIdAsync(caseId, CancellationToken.None);

            Assert.NotNull(record);
            Assert.Equal(createdAt, record.CreatedAtUtc);
            Assert.Equal(correlationId, record.UploadCorrelationId);
        }

        private static ErrorProcessingCaseRecord CreateCase(
            Guid id,
            ErrorProcessingStatus status,
            DateTime createdAtUtc,
            ErrorProcessingItemRecord[] items = null)
        {
            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = id,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = status,
                CreatedAtUtc = createdAtUtc,
                ExpiresAtUtc = createdAtUtc.AddDays(2),
                SourceFilePath = @"\\share\inbox\mvk\file.xlsx",
                Items = items ?? Array.Empty<ErrorProcessingItemRecord>()
            };
        }
    }
}
