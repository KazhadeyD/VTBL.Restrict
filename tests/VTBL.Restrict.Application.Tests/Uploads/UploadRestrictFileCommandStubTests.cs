using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Uploads;
using VTBL.Restrict.Application.Tests.Support;
using Xunit;

namespace VTBL.Restrict.Application.Tests.Uploads
{
    /// <summary>
    /// Upload command: реальная запись as-is после 2.2.
    /// </summary>
    public sealed class UploadRestrictFileCommandStubTests
    {
        [Fact]
        public async Task ExecuteAsync_ValidShell_WritesFileAndReturnsCorrelationId()
        {
            var (command, _) = UploadCommandTestSupport.CreateWithTempStorage();
            var payload = new byte[] { 1, 2, 3 };

            await using var stream = new MemoryStream(payload);
            var result = await command.ExecuteAsync(new UploadRestrictFileRequest
            {
                ListTypeCode = "MVK",
                OriginalFileName = "list.xlsx",
                ContentLength = payload.Length,
                Content = stream
            }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.CorrelationId);
            Assert.Equal("Файл успешно загружен и передан на обработку.", result.Message);
            Assert.True(File.Exists(result.StoredFilePath));
        }
    }
}
