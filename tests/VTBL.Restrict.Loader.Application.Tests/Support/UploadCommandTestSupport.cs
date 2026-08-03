using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Options;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Infrastructure.Files;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace VTBL.Restrict.Loader.Application.Tests.Support
{
    /// <summary>
    /// Фабрика command + temp RemoteRoot + notifier для тестов upload flow.
    /// </summary>
    public static class UploadCommandTestSupport
    {
        public static UploadTestHarness CreateHarness(bool notifierFails = false)
        {
            var remoteRoot = Path.Combine(
                Path.GetTempPath(),
                "VTBL.Restrict.Loader.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(remoteRoot);

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
                notifier);

            return new UploadTestHarness(command, remoteRoot, notifier);
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
            IUploadNotifier notifier)
        {
            Command = command;
            RemoteRoot = remoteRoot;
            Notifier = notifier;
        }

        public UploadRestrictFileCommand Command { get; }
        public string RemoteRoot { get; }
        public IUploadNotifier Notifier { get; }
    }

    public sealed class TrackingUploadNotifier : IUploadNotifier
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

    public sealed class OrderSequenceTracker
    {
        private int _counter;

        public int WriteOrder { get; private set; }
        public int PublishOrder { get; private set; }

        public void MarkWrite() => WriteOrder = ++_counter;
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
