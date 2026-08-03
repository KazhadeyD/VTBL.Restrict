using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.ErrorProcessing;
using VTBL.Restrict.Loader.Application.Observability;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Application.Tests.Support;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Infrastructure.Files;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;
using Abstractions = VTBL.Restrict.Loader.Application.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace VTBL.Restrict.Loader.Application.Tests.Observability
{
    public sealed class CorrelationLoggingTests
    {
        [Fact]
        public async Task TC_UNIT_01_OpenLogs_DoNotContainRawToken()
        {
            const string rawToken = "super-secret-token-XYZ-987654";
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(new Abstractions.ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                Items = Array.Empty<Abstractions.ErrorProcessingItemRecord>()
            });

            var provider = new CollectingLoggerProvider();
            var logger = new CollectingLogger<GetErrorProcessingForOperatorQuery>(provider);
            var query = new GetErrorProcessingForOperatorQuery(store, logger);

            await query.ExecuteAsync(caseId, rawToken, CancellationToken.None);

            Assert.NotEmpty(provider.Entries);
            Assert.All(provider.Entries, e => Assert.DoesNotContain(rawToken, e.Message));
            Assert.Contains(provider.Entries, e => e.Message.Contains("tokenPresent=present"));
            Assert.Contains(provider.Entries, e => e.Message.Contains(caseId.ToString()));
        }

        [Fact]
        public async Task TC_UNIT_02_SuccessUploadLog_ContainsListTypeAndCorrelationId()
        {
            var provider = new CollectingLoggerProvider();
            var logger = new CollectingLogger<UploadRestrictFileCommand>(provider);
            var harness = UploadCommandTestSupport.CreateHarness();
            var command = new UploadRestrictFileCommand(
                new InMemoryListTypeReadStore(),
                MsOptions.Create(new RestrictStorageOptions
                {
                    RemoteRoot = harness.RemoteRoot,
                    AllowedExtensions = new[] { ".xlsx", ".xls", ".csv" },
                    MaxFileSizeBytes = 52_428_800L
                }),
                new UploadPathBuilder(),
                new UncFileShareStore(),
                harness.BatchStore,
                harness.Notifier,
                logger);

            await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var result = await command.ExecuteAsync(new UploadRestrictFileRequest
            {
                ListTypeCode = "MVK",
                OriginalFileName = "list.xlsx",
                ContentLength = 3,
                Content = stream
            }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.CorrelationId);
            var joined = string.Join('\n', provider.Entries.Select(e => e.Message));
            Assert.Contains("Upload succeeded", joined);
            Assert.Contains("MVK", joined);
            Assert.Contains(result.CorrelationId.Value.ToString(), joined);
        }

        [Fact]
        public void SensitiveLog_NeverEchoesToken()
        {
            Assert.Equal("present", SensitiveLog.DescribeTokenPresence("abc"));
            Assert.Equal("absent", SensitiveLog.DescribeTokenPresence(null));
            Assert.DoesNotContain("abc", SensitiveLog.DescribeTokenPresence("abc"));
        }
    }
}
