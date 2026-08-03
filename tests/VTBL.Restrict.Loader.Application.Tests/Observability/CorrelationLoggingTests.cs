using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Application.Tests.Support;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Infrastructure.Files;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace VTBL.Restrict.Loader.Application.Tests.Observability
{
    public sealed class CorrelationLoggingTests
    {
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
    }
}
