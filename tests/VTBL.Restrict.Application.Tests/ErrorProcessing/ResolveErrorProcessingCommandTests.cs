using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.ErrorProcessing;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Application.Tests.ErrorProcessing
{
    public sealed class ResolveErrorProcessingCommandTests
    {
        [Fact]
        public async Task ExecuteAsync_Happy_ResolvesAndSavesUserValues()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreatePending(caseId, CreateItem(itemId, "F1", required: true, raw: "RAW", parser: "P", hash: new byte[] { 1, 2 })));

            var result = await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = caseId,
                    RawToken = "ignored",
                    ItemUserValues = new Dictionary<Guid, string> { [itemId] = "fixed" },
                    ResolvedBy = "op"
                },
                CancellationToken.None);

            Assert.True(result.Success);
            var after = await store.GetByIdAsync(caseId, CancellationToken.None);
            Assert.Equal(ErrorProcessingStatus.ResolvedByUser, after.Status);
            Assert.Equal("fixed", after.Items.Single().UserValue);
        }

        [Fact]
        public async Task ExecuteAsync_MissingRequired_Validation_KeepsPending()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreatePending(caseId, CreateItem(itemId, "REQ", required: true)));

            var result = await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = caseId,
                    ItemUserValues = new Dictionary<Guid, string> { [itemId] = "  " }
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ErrorProcessingErrorCodes.Validation, result.ErrorCode);
            Assert.Contains(itemId, result.FieldErrors.Keys);
            var after = await store.GetByIdAsync(caseId, CancellationToken.None);
            Assert.Equal(ErrorProcessingStatus.Pending, after.Status);
            Assert.Null(after.Items.Single().UserValue);
        }

        [Fact]
        public async Task ExecuteAsync_AlreadyResolved_Conflict()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            var record = CreatePending(caseId, CreateItem(itemId, "X", required: true));
            record.Status = ErrorProcessingStatus.ResolvedByUser;
            store.Seed(record);

            var result = await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = caseId,
                    ItemUserValues = new Dictionary<Guid, string> { [itemId] = "v" }
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ErrorProcessingErrorCodes.Conflict, result.ErrorCode);
        }

        [Fact]
        public async Task ExecuteAsync_NotFound()
        {
            var store = new InMemoryErrorProcessingCaseStore();
            var result = await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = Guid.NewGuid(),
                    ItemUserValues = new Dictionary<Guid, string>()
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ErrorProcessingErrorCodes.NotFound, result.ErrorCode);
        }

        [Fact]
        public async Task ExecuteAsync_IgnoresToken()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreatePending(caseId, CreateItem(itemId, "T", required: true)));

            var result = await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = caseId,
                    RawToken = "wrong-token",
                    ItemUserValues = new Dictionary<Guid, string> { [itemId] = "ok" }
                },
                CancellationToken.None);

            Assert.True(result.Success);
        }

        [Fact]
        public async Task TC_UNIT_01_MidResolveFailure_RollsBack_StatusPending()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreatePending(caseId, CreateItem(itemId, "F", required: true, raw: "RAW", parser: "MSG")));
            store.MidResolveFault = () => throw new InvalidOperationException("simulated mid-tx");

            var result = await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = caseId,
                    ItemUserValues = new Dictionary<Guid, string> { [itemId] = "would-save" }
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ErrorProcessingErrorCodes.Db, result.ErrorCode);
            var after = await store.GetByIdAsync(caseId, CancellationToken.None);
            Assert.Equal(ErrorProcessingStatus.Pending, after.Status);
            Assert.Null(after.Items.Single().UserValue);
            Assert.Equal("RAW", after.Items.Single().RawValue);
        }

        [Fact]
        public async Task TC_UNIT_02_Resolve_DoesNotTouch_RawValue_ParserMessage_AccessTokenHash()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var hash = new byte[] { 9, 8, 7 };
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(CreatePending(
                caseId,
                CreateItem(itemId, "F", required: true, raw: "KEEP_RAW", parser: "KEEP_MSG"),
                hash));

            await new ResolveErrorProcessingCommand(store).ExecuteAsync(
                new ResolveErrorProcessingRequest
                {
                    ErrorProcessingCaseId = caseId,
                    ItemUserValues = new Dictionary<Guid, string> { [itemId] = "operator" }
                },
                CancellationToken.None);

            var after = await store.GetByIdAsync(caseId, CancellationToken.None);
            Assert.Equal("KEEP_RAW", after.Items.Single().RawValue);
            Assert.Equal("KEEP_MSG", after.Items.Single().ParserMessage);
            Assert.Equal(hash, after.AccessTokenHash);
            Assert.Equal("operator", after.Items.Single().UserValue);
        }

        private static ErrorProcessingCaseRecord CreatePending(
            Guid caseId,
            ErrorProcessingItemRecord item,
            byte[] accessTokenHash = null)
        {
            return new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                SourceFilePath = @"\\share\a.xlsx",
                AccessTokenHash = accessTokenHash,
                Items = new[] { item }
            };
        }

        private static ErrorProcessingItemRecord CreateItem(
            Guid id,
            string field,
            bool required,
            string raw = "raw",
            string parser = "parser",
            byte[] hash = null)
        {
            _ = hash;
            return new ErrorProcessingItemRecord
            {
                ErrorProcessingItemId = id,
                FieldCode = field,
                RawValue = raw,
                ParserMessage = parser,
                UserValue = null,
                IsRequired = required,
                SortOrder = 1
            };
        }
    }
}
