using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Uploads;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Application.Tests.Uploads
{
    /// <summary>
    /// TC-UNIT (task 2.4): Retry без FileShare; ветки Status.
    /// </summary>
    public sealed class RetryUploadNotificationCommandTests
    {
        [Fact]
        public async Task ExecuteAsync_Failed_Republishes_UpdatesPublished_NoFileShare()
        {
            var correlationId = Guid.NewGuid();
            var batchStore = new InMemoryUploadBatchStore();
            var notifier = new TrackingNotifier();
            var fileShare = new CountingFileShareStore();

            await SeedBatchAsync(batchStore, correlationId, NotifyStatus.Failed);

            var command = new RetryUploadNotificationCommand(
                batchStore,
                new InMemoryListTypeReadStore(),
                notifier);

            var result = await command.ExecuteAsync(correlationId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(1, notifier.PublishCallCount);
            Assert.Equal("restrict.upload.mvk", notifier.LastRoutingKey);
            Assert.Equal(NotifyStatus.Published, (await batchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None)).NotifyStatus);
            Assert.Equal(0, fileShare.WriteCallCount);
            // Prove FileShare was never wired into command (constructor doesn't take it).
            Assert.Null(typeof(RetryUploadNotificationCommand).GetConstructor(new[]
            {
                typeof(IUploadBatchStore),
                typeof(IListTypeReadStore),
                typeof(IUploadNotifier),
                typeof(IFileShareStore)
            }));
        }

        [Fact]
        public async Task ExecuteAsync_Pending_Allowed_PayloadFromBatch()
        {
            var correlationId = Guid.NewGuid();
            var batchStore = new InMemoryUploadBatchStore();
            var notifier = new TrackingNotifier();
            var storedPath = Path.Combine(Path.GetTempPath(), "seed", correlationId.ToString("N") + "_list.xlsx");

            await SeedBatchAsync(batchStore, correlationId, NotifyStatus.Pending, storedPath, "list.xlsx");

            var command = new RetryUploadNotificationCommand(
                batchStore,
                new InMemoryListTypeReadStore(),
                notifier);

            var result = await command.ExecuteAsync(correlationId, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(correlationId, notifier.LastMessage.CorrelationId);
            Assert.Equal("MVK", notifier.LastMessage.ListType);
            Assert.Equal(storedPath, notifier.LastMessage.FilePath);
            Assert.Equal("list.xlsx", notifier.LastMessage.OriginalFileName);
            Assert.Equal("restrict.upload.mvk", notifier.LastRoutingKey);
            Assert.Equal(NotifyStatus.Published, (await batchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None)).NotifyStatus);
        }

        [Fact]
        public async Task ExecuteAsync_Published_RejectsWithoutPublish()
        {
            var correlationId = Guid.NewGuid();
            var batchStore = new InMemoryUploadBatchStore();
            var notifier = new TrackingNotifier();

            await SeedBatchAsync(batchStore, correlationId, NotifyStatus.Pending);
            await batchStore.UpdateNotifyStatusAsync(correlationId, NotifyStatus.Published, CancellationToken.None);

            var command = new RetryUploadNotificationCommand(
                batchStore,
                new InMemoryListTypeReadStore(),
                notifier);

            var result = await command.ExecuteAsync(correlationId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(UploadErrorCodes.Conflict, result.ErrorCode);
            Assert.Equal(0, notifier.PublishCallCount);
            Assert.Equal(NotifyStatus.Published, (await batchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None)).NotifyStatus);
        }

        [Fact]
        public async Task ExecuteAsync_UnknownCorrelation_NotFound()
        {
            var command = new RetryUploadNotificationCommand(
                new InMemoryUploadBatchStore(),
                new InMemoryListTypeReadStore(),
                new TrackingNotifier());

            var result = await command.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(UploadErrorCodes.NotFound, result.ErrorCode);
        }

        [Fact]
        public async Task ExecuteAsync_PublishFail_MarksFailed()
        {
            var correlationId = Guid.NewGuid();
            var batchStore = new InMemoryUploadBatchStore();
            await SeedBatchAsync(batchStore, correlationId, NotifyStatus.Failed);

            var command = new RetryUploadNotificationCommand(
                batchStore,
                new InMemoryListTypeReadStore(),
                new FailingNotifier());

            var result = await command.ExecuteAsync(correlationId, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(UploadErrorCodes.Rmq, result.ErrorCode);
            Assert.Equal(NotifyStatus.Failed, (await batchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None)).NotifyStatus);
        }

        private static async Task SeedBatchAsync(
            InMemoryUploadBatchStore store,
            Guid correlationId,
            NotifyStatus status,
            string storedPath = null,
            string originalName = "list.xlsx")
        {
            await store.InsertPendingAsync(new UploadBatchRecord
            {
                CorrelationId = correlationId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                OriginalFileName = originalName,
                StoredFilePath = storedPath ?? @"C:\inbox\mvk\file.xlsx",
                UploadedBy = "tester",
                UploadedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            }, CancellationToken.None);

            if (status != NotifyStatus.Pending)
            {
                await store.UpdateNotifyStatusAsync(correlationId, status, CancellationToken.None);
            }
        }

        private sealed class TrackingNotifier : IUploadNotifier
        {
            public int PublishCallCount { get; private set; }
            public RestrictFileUploadedMessage LastMessage { get; private set; }
            public string LastRoutingKey { get; private set; }

            public Task PublishUploadedAsync(
                RestrictFileUploadedMessage message,
                string routingKey,
                CancellationToken cancellationToken)
            {
                PublishCallCount++;
                LastMessage = message;
                LastRoutingKey = routingKey;
                return Task.CompletedTask;
            }
        }

        private sealed class FailingNotifier : IUploadNotifier
        {
            public Task PublishUploadedAsync(
                RestrictFileUploadedMessage message,
                string routingKey,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("RMQ down");
            }
        }

        private sealed class CountingFileShareStore : IFileShareStore
        {
            public int WriteCallCount { get; private set; }

            public Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken)
            {
                WriteCallCount++;
                return Task.FromResult(targetFullPath);
            }
        }
    }
}
