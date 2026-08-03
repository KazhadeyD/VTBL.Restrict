using System;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.ErrorProcessing;
using VTBL.Restrict.Loader.Application.Tests.Support;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Loader.Application.Tests.ErrorProcessing
{
    /// <summary>
    /// Unit-тесты списка Error Processing (реальная оркестрация List-query).
    /// </summary>
    public sealed class ListPendingErrorProcessingCasesQueryTests
    {
        /// <summary>
        /// TC-UNIT-01: success maps fields; empty store → empty success.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_EmptyStore_ReturnsSucceededWithEmptyItems()
        {
            var query = new ListPendingErrorProcessingCasesQuery(new InMemoryErrorProcessingCaseStore());

            var result = await query.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Null(result.ErrorCode);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Items);
            Assert.Empty(result.Items);
        }

        /// <summary>
        /// TC-UNIT-01: Pending в store → map DTO + order DESC; Resolved исключён; ExpiresAt не фильтрует.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_MapsSeededPending_OrderedByCreatedAtDesc()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var older = Guid.NewGuid();
            var newer = Guid.NewGuid();
            var pastExpires = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            store.Seed(CreateCase(
                older,
                ErrorProcessingStatus.Pending,
                createdAtUtc: new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc),
                sourcePath: @"\\share\old.xlsx",
                expiresAtUtc: pastExpires));
            store.Seed(CreateCase(
                newer,
                ErrorProcessingStatus.Pending,
                createdAtUtc: new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                sourcePath: @"\\share\new.xlsx"));
            store.Seed(CreateCase(
                Guid.NewGuid(),
                ErrorProcessingStatus.ResolvedByUser,
                createdAtUtc: new DateTime(2026, 7, 15, 18, 0, 0, DateTimeKind.Utc)));

            var query = new ListPendingErrorProcessingCasesQuery(store);
            var result = await query.ExecuteAsync(CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(newer, result.Items[0].ErrorProcessingCaseId);
            Assert.Equal(older, result.Items[1].ErrorProcessingCaseId);
            Assert.Equal("MVK", result.Items[0].ListTypeCode);
            Assert.Equal("МВК", result.Items[0].ListTypeName);
            Assert.Equal(ErrorProcessingStatus.Pending, result.Items[0].Status);
            Assert.Equal(@"\\share\new.xlsx", result.Items[0].SourceFilePath);
            Assert.Equal(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc), result.Items[0].CreatedAtUtc);
            Assert.Equal(pastExpires, result.Items[1].ExpiresAtUtc);
            Assert.True(result.Items[1].ExpiresAtUtc < result.Items[0].CreatedAtUtc);
        }

        /// <summary>
        /// TC-UNIT-02: store throws → Failed + Db; лог без UserValue.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_StoreThrows_ReturnsFailedDb_WithoutUserValueInLogs()
        {
            const string secretUserValue = "PII-USER-VALUE-SECRET";
            var provider = new CollectingLoggerProvider();
            var logger = new CollectingLogger<ListPendingErrorProcessingCasesQuery>(provider);
            var query = new ListPendingErrorProcessingCasesQuery(
                new ThrowingListErrorProcessingStore(secretUserValue),
                logger);

            var result = await query.ExecuteAsync(CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(ErrorProcessingErrorCodes.Db, result.ErrorCode);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.Empty(result.Items);
            Assert.Contains(provider.Entries, e => e.Message.Contains("list failed", StringComparison.OrdinalIgnoreCase)
                || e.Message.Contains("ErrorProcessing list failed"));
            Assert.All(provider.Entries, e => Assert.DoesNotContain(secretUserValue, e.Message));
            Assert.All(provider.Entries, e => Assert.DoesNotContain("UserValue", e.Message));
        }

        /// <summary>
        /// Регресс store: InMemory ListPendingSummariesAsync на пустом store → empty.
        /// </summary>
        [Fact]
        public async Task ListPendingSummariesAsync_EmptyStore_ReturnsEmpty()
        {
            var store = new InMemoryErrorProcessingCaseStore();

            var items = await store.ListPendingSummariesAsync(CancellationToken.None);

            Assert.NotNull(items);
            Assert.Empty(items);
        }

        /// <summary>
        /// Регресс store: только Pending; CreatedAtUtc DESC.
        /// </summary>
        [Fact]
        public async Task ListPendingSummariesAsync_FiltersPending_OrdersByCreatedAtDesc()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var olderPending = Guid.NewGuid();
            var newerPending = Guid.NewGuid();
            var resolved = Guid.NewGuid();

            store.Seed(CreateCase(olderPending, ErrorProcessingStatus.Pending,
                createdAtUtc: new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
            store.Seed(CreateCase(newerPending, ErrorProcessingStatus.Pending,
                createdAtUtc: new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)));
            store.Seed(CreateCase(resolved, ErrorProcessingStatus.ResolvedByUser,
                createdAtUtc: new DateTime(2026, 7, 15, 18, 0, 0, DateTimeKind.Utc)));

            var items = await store.ListPendingSummariesAsync(CancellationToken.None);

            Assert.Equal(2, items.Count);
            Assert.Equal(newerPending, items[0].ErrorProcessingCaseId);
            Assert.Equal(olderPending, items[1].ErrorProcessingCaseId);
            Assert.DoesNotContain(items, x => x.ErrorProcessingCaseId == resolved);
            Assert.All(items, x => Assert.Equal(ErrorProcessingStatus.Pending, x.Status));
        }

        private static ErrorProcessingCaseRecord CreateCase(
            Guid id,
            ErrorProcessingStatus status,
            DateTime createdAtUtc,
            string sourcePath = @"\\share\inbox\mvk\file.xlsx",
            DateTime? expiresAtUtc = null)
        {
            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = id,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = status,
                CreatedAtUtc = createdAtUtc,
                ExpiresAtUtc = expiresAtUtc ?? createdAtUtc.AddDays(2),
                SourceFilePath = sourcePath,
                Items = Array.Empty<ErrorProcessingItemRecord>()
            };
        }

        private sealed class ThrowingListErrorProcessingStore : IErrorProcessingStore
        {
            private readonly string _secret;

            public ThrowingListErrorProcessingStore(string secret) => _secret = secret;

            public Task<ErrorProcessingCaseRecord> GetByIdAsync(
                Guid errorProcessingCaseId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<System.Collections.Generic.IReadOnlyList<ErrorProcessingCaseSummaryRecord>> ListPendingSummariesAsync(
                CancellationToken cancellationToken)
            {
                _ = _secret;
                throw new InvalidOperationException("simulated list db failure");
            }

            public Task<bool> ResolveAsync(
                Guid errorProcessingCaseId,
                System.Collections.Generic.IReadOnlyDictionary<Guid, string> itemUserValues,
                string resolvedBy,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }
    }
}
