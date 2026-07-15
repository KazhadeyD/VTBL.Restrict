using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Options;
using VTBL.Restrict.Application.Uploads;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Infrastructure.Files;
using VTBL.Restrict.Infrastructure.Stub;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace VTBL.Restrict.Application.Tests.Support
{
    /// <summary>
    /// Фабрика command + temp RemoteRoot + in-memory batch/notifier для тестов upload flow.
    /// </summary>
    public static class UploadCommandTestSupport
    {
        public static UploadTestHarness CreateHarness(bool notifierFails = false, bool insertFails = false)
        {
            var remoteRoot = Path.Combine(
                Path.GetTempPath(),
                "VTBL.Restrict.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(remoteRoot);

            var batchStore = insertFails
                ? (IUploadBatchStore)new FailingInsertUploadBatchStore()
                : new InMemoryUploadBatchStore();

            var notifier = notifierFails
                ? (IUploadNotifier)new FailingUploadNotifier()
                : new TrackingUploadNotifier();

            var options = MsOptions.Create(new RestrictStorageOptions
            {
                RemoteRoot = remoteRoot,
                AllowedExtensions = new[] { ".xlsx", ".xls", ".csv" },
                MaxFileSizeBytes = 52_428_800L
            });

            var command = new UploadRestrictFileCommand(
                new InMemoryListTypeReadStore(),
                options,
                new UploadPathBuilder(),
                new UncFileShareStore(),
                batchStore,
                notifier);

            return new UploadTestHarness(command, remoteRoot, batchStore, notifier);
        }

        public static (UploadRestrictFileCommand Command, string RemoteRoot) CreateWithTempStorage()
        {
            var harness = CreateHarness();
            return (harness.Command, harness.RemoteRoot);
        }
    }

    public sealed class UploadTestHarness
    {
        public UploadTestHarness(
            UploadRestrictFileCommand command,
            string remoteRoot,
            IUploadBatchStore batchStore,
            IUploadNotifier notifier)
        {
            Command = command;
            RemoteRoot = remoteRoot;
            BatchStore = batchStore;
            Notifier = notifier;
        }

        public UploadRestrictFileCommand Command { get; }
        public string RemoteRoot { get; }
        public IUploadBatchStore BatchStore { get; }
        public IUploadNotifier Notifier { get; }
    }

    public sealed class TrackingUploadNotifier : IUploadNotifier
    {
        public int PublishCallCount { get; private set; }
        public RestrictFileUploadedMessage LastMessage { get; private set; }
        public string LastRoutingKey { get; private set; }
        public int WriteCallOrderMarker { get; set; }
        public int PublishCallOrderMarker { get; private set; }

        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            PublishCallCount++;
            PublishCallOrderMarker = WriteCallOrderMarker + 1;
            LastMessage = message;
            LastRoutingKey = routingKey;
            return Task.CompletedTask;
        }
    }

    public sealed class FailingUploadNotifier : IUploadNotifier
    {
        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated RMQ publish failure.");
        }
    }

    public sealed class FailingInsertUploadBatchStore : IUploadBatchStore
    {
        public Task InsertPendingAsync(UploadBatchRecord batch, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated DB insert failure.");
        }

        public Task UpdateNotifyStatusAsync(Guid correlationId, NotifyStatus status, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<UploadBatchRecord> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            return Task.FromResult<UploadBatchRecord>(null);
        }
    }

    public sealed class OrderTrackingFileShareStore : IFileShareStore
    {
        private readonly IFileShareStore _inner;
        public OrderSequenceTracker Tracker { get; }

        public OrderTrackingFileShareStore(IFileShareStore inner, OrderSequenceTracker tracker)
        {
            _inner = inner;
            Tracker = tracker;
        }

        public async Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken)
        {
            Tracker.MarkWrite();
            return await _inner.WriteAsIsAsync(content, targetFullPath, cancellationToken);
        }
    }

    public sealed class OrderTrackingUploadBatchStore : IUploadBatchStore
    {
        private readonly InMemoryUploadBatchStore _inner = new InMemoryUploadBatchStore();
        private readonly OrderSequenceTracker _tracker;

        public OrderTrackingUploadBatchStore(OrderSequenceTracker tracker)
        {
            _tracker = tracker;
        }

        public async Task InsertPendingAsync(UploadBatchRecord batch, CancellationToken cancellationToken)
        {
            _tracker.MarkInsert();
            await _inner.InsertPendingAsync(batch, cancellationToken);
        }

        public Task UpdateNotifyStatusAsync(Guid correlationId, NotifyStatus status, CancellationToken cancellationToken)
        {
            return _inner.UpdateNotifyStatusAsync(correlationId, status, cancellationToken);
        }

        public Task<UploadBatchRecord> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            return _inner.GetByCorrelationIdAsync(correlationId, cancellationToken);
        }

        public InMemoryUploadBatchStore Inner => _inner;
    }

    public sealed class OrderSequenceTracker
    {
        private int _counter;

        public int WriteOrder { get; private set; }
        public int InsertOrder { get; private set; }
        public int PublishOrder { get; set; }

        public void MarkWrite() => WriteOrder = ++_counter;
        public void MarkInsert() => InsertOrder = ++_counter;
        public void MarkPublish() => PublishOrder = ++_counter;
    }

    public sealed class TrackingUploadNotifierWithOrder : IUploadNotifier
    {
        private readonly OrderSequenceTracker _tracker;

        public TrackingUploadNotifierWithOrder(OrderSequenceTracker tracker)
        {
            _tracker = tracker;
        }

        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            _tracker.MarkPublish();
            return Task.CompletedTask;
        }
    }
}
