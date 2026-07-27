using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.ErrorProcessing;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Application.Tests.ErrorProcessing
{
    /// <summary>
    /// TC-UNIT: Get by caseId, SortOrder, Db-failure vs status refusals.
    /// </summary>
    public sealed class GetErrorProcessingForOperatorQueryTests
    {
        [Fact]
        public async Task ExecuteAsync_Pending_MapsItemsBySortOrder()
        {
            var caseId = Guid.NewGuid();
            var createdAt = new DateTime(2026, 7, 10, 8, 0, 0, DateTimeKind.Utc);
            var correlationId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(
                caseId,
                ErrorProcessingStatus.Pending,
                new[]
                {
                    CreateItem(Guid.NewGuid(), "B_FIELD", 20),
                    CreateItem(Guid.NewGuid(), "A_FIELD", 10)
                },
                createdAtUtc: createdAt,
                uploadCorrelationId: correlationId));

            var query = new GetErrorProcessingForOperatorQuery(store);
            var result = await query.ExecuteAsync(caseId, "ignored-token", CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(result.View.IsReadOnly);
            Assert.Equal(new[] { "A_FIELD", "B_FIELD" }, result.View.Items.Select(i => i.FieldCode).ToArray());
            Assert.Equal(new[] { 10, 20 }, result.View.Items.Select(i => i.SortOrder).ToArray());
            Assert.Equal(createdAt, result.View.CreatedAtUtc);
            Assert.Equal(correlationId, result.View.UploadCorrelationId);
        }

        [Fact]
        public async Task ExecuteAsync_NotFound_ReturnsNotFound()
        {
            var query = new GetErrorProcessingForOperatorQuery(new InMemoryErrorProcessingCaseStore());
            var result = await query.ExecuteAsync(Guid.NewGuid(), null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonNotFound, result.FailureReason);
        }

        [Fact]
        public async Task ExecuteAsync_Resolved_IsReadOnly()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(caseId, ErrorProcessingStatus.ResolvedByUser, new[]
            {
                CreateItem(Guid.NewGuid(), "F1", 1)
            }));

            var result = await new GetErrorProcessingForOperatorQuery(store)
                .ExecuteAsync(caseId, "any", CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.True(result.View.IsReadOnly);
            Assert.Equal(ErrorProcessingStatus.ResolvedByUser, result.View.Status);
        }

        [Fact]
        public async Task ExecuteAsync_Cancelled_Unavailable()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(caseId, ErrorProcessingStatus.Cancelled, Array.Empty<ErrorProcessingItemRecord>()));

            var result = await new GetErrorProcessingForOperatorQuery(store)
                .ExecuteAsync(caseId, null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonUnavailable, result.FailureReason);
        }

        /// <summary>
        /// TC-UNIT-04: Expired → Unavailable (регресс); не путать с Db.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_Expired_Unavailable_NotDb()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(caseId, ErrorProcessingStatus.Expired, Array.Empty<ErrorProcessingItemRecord>()));

            var result = await new GetErrorProcessingForOperatorQuery(store)
                .ExecuteAsync(caseId, null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonUnavailable, result.FailureReason);
            Assert.NotEqual(ErrorProcessingCaseAccessResult.ReasonDb, result.FailureReason);
        }

        /// <summary>
        /// TC-UNIT-03: store throws → DbError; Expired/Cancelled → Unavailable (не Db); Pending — Ok.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_StoreThrows_ReturnsDbError_DistinctFromUnavailable()
        {
            var pendingId = Guid.NewGuid();
            var expiredId = Guid.NewGuid();
            var cancelledId = Guid.NewGuid();
            var inner = new InMemoryErrorProcessingCaseStore();
            inner.Seed(CreateCase(pendingId, ErrorProcessingStatus.Pending, new[]
            {
                CreateItem(Guid.NewGuid(), "OK", 1)
            }));
            inner.Seed(CreateCase(expiredId, ErrorProcessingStatus.Expired, Array.Empty<ErrorProcessingItemRecord>()));
            inner.Seed(CreateCase(cancelledId, ErrorProcessingStatus.Cancelled, Array.Empty<ErrorProcessingItemRecord>()));

            var throwing = new ThrowingGetErrorProcessingStore(inner, throwForCaseId: Guid.NewGuid());
            var query = new GetErrorProcessingForOperatorQuery(throwing);

            var dbResult = await query.ExecuteAsync(throwing.FaultCaseId, "secret-token-XYZ", CancellationToken.None);
            Assert.False(dbResult.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonDb, dbResult.FailureReason);
            Assert.Null(dbResult.View);

            var expired = await query.ExecuteAsync(expiredId, null, CancellationToken.None);
            Assert.False(expired.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonUnavailable, expired.FailureReason);
            Assert.NotEqual(ErrorProcessingCaseAccessResult.ReasonDb, expired.FailureReason);

            var cancelled = await query.ExecuteAsync(cancelledId, null, CancellationToken.None);
            Assert.False(cancelled.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonUnavailable, cancelled.FailureReason);

            var ok = await query.ExecuteAsync(pendingId, null, CancellationToken.None);
            Assert.True(ok.Succeeded);
            Assert.Equal(ErrorProcessingStatus.Pending, ok.View.Status);
            Assert.False(ok.View.IsReadOnly);
        }

        [Fact]
        public async Task ExecuteAsync_TokenIgnored_SameForDifferentTokens()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(caseId, ErrorProcessingStatus.Pending, new[]
            {
                CreateItem(Guid.NewGuid(), "F", 1)
            }));

            var query = new GetErrorProcessingForOperatorQuery(store);
            var a = await query.ExecuteAsync(caseId, "token-a", CancellationToken.None);
            var b = await query.ExecuteAsync(caseId, "token-b", CancellationToken.None);

            Assert.True(a.Succeeded);
            Assert.True(b.Succeeded);
            Assert.Equal(a.View.ErrorProcessingCaseId, b.View.ErrorProcessingCaseId);
        }

        [Fact]
        public void Query_DoesNotDependOnFileShareStore()
        {
            var ctor = typeof(GetErrorProcessingForOperatorQuery).GetConstructors().Single();
            Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(IFileShareStore));
            Assert.Null(typeof(GetErrorProcessingForOperatorQuery).GetField(
                "_fileShareStore",
                BindingFlags.Instance | BindingFlags.NonPublic));
        }

        private static ErrorProcessingCaseRecord CreateCase(
            Guid id,
            ErrorProcessingStatus status,
            IEnumerable<ErrorProcessingItemRecord> items,
            DateTime? createdAtUtc = null,
            Guid? uploadCorrelationId = null)
        {
            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = id,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = status,
                CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow.AddHours(-1),
                UploadCorrelationId = uploadCorrelationId,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                SourceFilePath = @"\\parser\stored\path.xlsx",
                Items = items.ToList()
            };
        }

        private static ErrorProcessingItemRecord CreateItem(Guid id, string field, int sort)
        {
            return new ErrorProcessingItemRecord
            {
                ErrorProcessingItemId = id,
                FieldCode = field,
                RawValue = "raw",
                ParserMessage = "msg",
                IsRequired = true,
                SortOrder = sort
            };
        }

        /// <summary>
        /// Store: GetById throws for FaultCaseId; иначе делегирует inner.
        /// </summary>
        private sealed class ThrowingGetErrorProcessingStore : IErrorProcessingStore
        {
            private readonly IErrorProcessingStore _inner;

            public ThrowingGetErrorProcessingStore(IErrorProcessingStore inner, Guid throwForCaseId)
            {
                _inner = inner;
                FaultCaseId = throwForCaseId;
            }

            public Guid FaultCaseId { get; }

            public Task<ErrorProcessingCaseRecord> GetByIdAsync(
                Guid errorProcessingCaseId,
                CancellationToken cancellationToken)
            {
                if (errorProcessingCaseId == FaultCaseId)
                {
                    throw new InvalidOperationException("simulated get db failure");
                }

                return _inner.GetByIdAsync(errorProcessingCaseId, cancellationToken);
            }

            public Task<IReadOnlyList<ErrorProcessingCaseSummaryRecord>> ListPendingSummariesAsync(
                CancellationToken cancellationToken) =>
                _inner.ListPendingSummariesAsync(cancellationToken);

            public Task<bool> ResolveAsync(
                Guid errorProcessingCaseId,
                IReadOnlyDictionary<Guid, string> itemUserValues,
                string resolvedBy,
                CancellationToken cancellationToken) =>
                _inner.ResolveAsync(errorProcessingCaseId, itemUserValues, resolvedBy, cancellationToken);
        }
    }
}
