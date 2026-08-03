using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Application.Tests.Support;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Loader.Application.Tests.Stubs
{
    /// <summary>
    /// Stub Result codes для команд/queries. Upload после 2.2 — реальная запись в temp RemoteRoot.
    /// </summary>
    public sealed class ApplicationStubCommandsTests
    {
        [Fact]
        public async Task UploadRestrictFileCommand_ValidShell_WritesFileAndReturnsSuccess()
        {
            var (command, remoteRoot) = UploadCommandTestSupport.CreateWithTempStorage();
            var payload = new byte[] { 9, 8, 7 };

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
            Assert.False(string.IsNullOrEmpty(result.StoredFilePath));
            Assert.True(File.Exists(result.StoredFilePath));
            Assert.Equal(payload, await File.ReadAllBytesAsync(result.StoredFilePath));
            Assert.StartsWith(remoteRoot, result.StoredFilePath);
        }

        [Fact]
        public async Task UploadRestrictFileCommand_BadExtension_FailsWithoutSuccess()
        {
            var (command, _) = UploadCommandTestSupport.CreateWithTempStorage();
            var result = await command.ExecuteAsync(new UploadRestrictFileRequest
            {
                ListTypeCode = "MVK",
                OriginalFileName = "x.exe",
                ContentLength = 1
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Validation", result.ErrorCode);
        }

        [Fact]
        public async Task RetryUploadNotificationCommand_Failed_Republishes()
        {
            var correlationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var batchStore = new InMemoryUploadBatchStore();
            await batchStore.InsertPendingAsync(new UploadBatchRecord
            {
                CorrelationId = correlationId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                OriginalFileName = "a.xlsx",
                StoredFilePath = @"C:\a.xlsx",
                UploadedAtUtc = DateTime.UtcNow
            }, CancellationToken.None);
            await batchStore.UpdateNotifyStatusAsync(correlationId, NotifyStatus.Failed, CancellationToken.None);

            var command = new RetryUploadNotificationCommand(
                batchStore,
                new InMemoryListTypeReadStore(),
                new TrackingUploadNotifier());

            var result = await command.ExecuteAsync(correlationId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(correlationId, result.CorrelationId);
            Assert.Contains("повторно", result.Message);
        }
    }
}
