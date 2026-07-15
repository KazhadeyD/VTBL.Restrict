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
    /// TC-UNIT (task 3.1): Get by caseId, SortOrder, no filesystem.
    /// </summary>
    public sealed class GetErrorProcessingForOperatorQueryTests
    {
        [Fact]
        public async Task ExecuteAsync_Pending_MapsItemsBySortOrder()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(
                caseId,
                ErrorProcessingStatus.Pending,
                new[]
                {
                    CreateItem(Guid.NewGuid(), "B_FIELD", 20),
                    CreateItem(Guid.NewGuid(), "A_FIELD", 10)
                }));

            var query = new GetErrorProcessingForOperatorQuery(store);
            var result = await query.ExecuteAsync(caseId, "ignored-token", CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.False(result.View.IsReadOnly);
            Assert.Equal(new[] { "A_FIELD", "B_FIELD" }, result.View.Items.Select(i => i.FieldCode).ToArray());
            Assert.Equal(new[] { 10, 20 }, result.View.Items.Select(i => i.SortOrder).ToArray());
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

        [Fact]
        public async Task ExecuteAsync_Expired_Unavailable()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreateCase(caseId, ErrorProcessingStatus.Expired, Array.Empty<ErrorProcessingItemRecord>()));

            var result = await new GetErrorProcessingForOperatorQuery(store)
                .ExecuteAsync(caseId, null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Equal(ErrorProcessingCaseAccessResult.ReasonUnavailable, result.FailureReason);
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
            IEnumerable<ErrorProcessingItemRecord> items)
        {
            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = id,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = status,
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
    }
}
