using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Application.Tests.Support;
using VTBL.Restrict.Loader.Infrastructure.Files;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace VTBL.Restrict.Loader.Application.Tests.Uploads
{
    /// <summary>
    /// TC-UNIT (task 2.3): порядок EC-08 и ветки ошибок upload flow.
    /// </summary>
    public sealed class UploadRestrictFileCommandFlowTests
    {
        [Fact]
        public async Task ExecuteAsync_HappyPath_CallOrder_WriteInsertPublish()
        {
            var remoteRoot = CreateTempRoot();
            var tracker = new OrderSequenceTracker();
            var batchTracker = new OrderTrackingUploadBatchStore(tracker);
            var command = new UploadRestrictFileCommand(
                new InMemoryListTypeReadStore(),
                MsOptions.Create(CreateOptions(remoteRoot)),
                new UploadPathBuilder(),
                new OrderTrackingFileShareStore(new UncFileShareStore(), tracker),
                batchTracker,
                new TrackingUploadNotifierWithOrder(tracker));

            await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var result = await command.ExecuteAsync(CreateRequest(stream), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, tracker.WriteOrder);
            Assert.Equal(2, tracker.InsertOrder);
            Assert.Equal(3, tracker.PublishOrder);
            Assert.Equal(NotifyStatus.Published, (await batchTracker.Inner.GetByCorrelationIdAsync(result.CorrelationId.Value, CancellationToken.None)).NotifyStatus);
        }

        [Fact]
        public async Task ExecuteAsync_InsertFailAfterWrite_NoPublish()
        {
            var remoteRoot = CreateTempRoot();
            var notifier = new TrackingUploadNotifier();
            var command = new UploadRestrictFileCommand(
                new InMemoryListTypeReadStore(),
                MsOptions.Create(CreateOptions(remoteRoot)),
                new UploadPathBuilder(),
                new UncFileShareStore(),
                new FailingInsertUploadBatchStore(),
                notifier);

            await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            var result = await command.ExecuteAsync(CreateRequest(stream), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(UploadErrorCodes.Db, result.ErrorCode);
            Assert.NotNull(result.CorrelationId);
            Assert.True(File.Exists(result.StoredFilePath));
            Assert.Equal(0, notifier.PublishCallCount);
        }

        [Fact]
        public async Task ExecuteAsync_PublishFailAfterWriteAndInsert_NotifyStatusFailed_FileRemains()
        {
            var harness = UploadCommandTestSupport.CreateHarness(notifierFails: true);
            await using var stream = new MemoryStream(new byte[] { 4, 5, 6 });
            var result = await harness.Command.ExecuteAsync(CreateRequest(stream), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(UploadErrorCodes.Rmq, result.ErrorCode);
            Assert.True(File.Exists(result.StoredFilePath));

            var batch = await harness.BatchStore.GetByCorrelationIdAsync(result.CorrelationId.Value, CancellationToken.None);
            Assert.NotNull(batch);
            Assert.Equal(NotifyStatus.Failed, batch.NotifyStatus);
        }

        [Fact]
        public async Task ExecuteAsync_PublishPayload_ContainsRequiredFields()
        {
            var harness = UploadCommandTestSupport.CreateHarness();
            var tracking = (TrackingUploadNotifier)harness.Notifier;

            await using var stream = new MemoryStream(new byte[] { 7, 8, 9 });
            var result = await harness.Command.ExecuteAsync(CreateRequest(stream), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, tracking.PublishCallCount);
            Assert.Equal(result.CorrelationId, tracking.LastMessage.CorrelationId);
            Assert.Equal("MVK", tracking.LastMessage.ListType);
            Assert.Equal(result.StoredFilePath, tracking.LastMessage.FilePath);
            Assert.Equal("list.xlsx", tracking.LastMessage.OriginalFileName);
            Assert.Equal("restrict.upload.mvk", tracking.LastRoutingKey);
            Assert.True(tracking.LastMessage.UploadedAtUtc <= System.DateTime.UtcNow);
        }

        [Fact]
        public async Task ExecuteAsync_ShareFail_NoBatchNoPublish()
        {
            var remoteRoot = CreateTempRoot();
            var notifier = new TrackingUploadNotifier();
            var command = new UploadRestrictFileCommand(
                new InMemoryListTypeReadStore(),
                MsOptions.Create(CreateOptions(remoteRoot)),
                new UploadPathBuilder(),
                new FailingFileShareStoreForUnit(),
                new InMemoryUploadBatchStore(),
                notifier);

            await using var stream = new MemoryStream(new byte[] { 1 });
            var result = await command.ExecuteAsync(CreateRequest(stream), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(UploadErrorCodes.Share, result.ErrorCode);
            Assert.Null(result.CorrelationId);
            Assert.Equal(0, notifier.PublishCallCount);
        }

        private static UploadRestrictFileRequest CreateRequest(Stream stream)
        {
            return new UploadRestrictFileRequest
            {
                ListTypeCode = "MVK",
                OriginalFileName = "list.xlsx",
                ContentLength = stream.Length,
                Content = stream,
                UploadedBy = "tester"
            };
        }

        private static RestrictStorageOptions CreateOptions(string remoteRoot)
        {
            return new RestrictStorageOptions
            {
                RemoteRoot = remoteRoot,
                AllowedExtensions = new[] { ".xlsx", ".xls", ".csv" },
                MaxFileSizeBytes = 52_428_800L
            };
        }

        private static string CreateTempRoot()
        {
            var path = Path.Combine(Path.GetTempPath(), "VTBL.Restrict.Loader.Tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class FailingFileShareStoreForUnit : IFileShareStore
        {
            public Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken)
            {
                throw new IOException("Simulated share failure.");
            }
        }
    }
}
