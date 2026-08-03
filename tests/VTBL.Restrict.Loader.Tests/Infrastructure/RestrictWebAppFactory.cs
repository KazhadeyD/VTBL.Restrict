using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using VTBL.Restrict.Loader.Tests.Fakes;
using VTBL.Restrict.Loader.UI;

namespace VTBL.Restrict.Loader.Tests.Infrastructure
{
    /// <summary>
    /// Test host: InMemory ListType/Batch + tracking notifier + temp RemoteRoot.
    /// </summary>
    public sealed class RestrictWebAppFactory : WebApplicationFactory<Program>
    {
        public RestrictWebAppFactory()
        {
            TestRemoteRoot = Path.Combine(
                Path.GetTempPath(),
                "VTBL.Restrict.Loader.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TestRemoteRoot);
            UploadBatchStore = new InMemoryUploadBatchStore();
            TrackingNotifier = new TrackingUploadNotifier();
            ErrorProcessingStore = new InMemoryErrorProcessingCaseStore();
        }

        public string TestRemoteRoot { get; }

        public InMemoryUploadBatchStore UploadBatchStore { get; }

        public InMemoryErrorProcessingCaseStore ErrorProcessingStore { get; }

        public TrackingUploadNotifier TrackingNotifier { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:RestrictDb"] = string.Empty,
                    ["RestrictStorage:RemoteRoot"] = TestRemoteRoot,
                    ["RabbitMq:Host"] = string.Empty
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IListTypeReadStore>();
                services.AddSingleton<IListTypeReadStore>(_ => new InMemoryListTypeReadStore());

                services.RemoveAll<IUploadBatchStore>();
                services.AddSingleton<IUploadBatchStore>(UploadBatchStore);

                services.RemoveAll<IUploadNotifier>();
                services.AddSingleton<IUploadNotifier>(TrackingNotifier);

                services.RemoveAll<IErrorProcessingStore>();
                services.AddSingleton<IErrorProcessingStore>(ErrorProcessingStore);
            });
        }

        public void TryCleanupRemoteRoot()
        {
            try
            {
                if (Directory.Exists(TestRemoteRoot))
                {
                    Directory.Delete(TestRemoteRoot, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>
    /// Notifier с фиксацией порядка относительно file write (E2E EC-08).
    /// </summary>
    public sealed class TrackingUploadNotifier : IUploadNotifier
    {
        public int PublishCallCount { get; private set; }

        public bool PublishAfterWrite { get; private set; }

        public int LastWriteMarkerSeen { get; set; }

        public Task PublishUploadedAsync(
            RestrictFileUploadedMessage message,
            string routingKey,
            CancellationToken cancellationToken)
        {
            PublishCallCount++;
            PublishAfterWrite = LastWriteMarkerSeen > 0;
            return Task.CompletedTask;
        }

        public void Reset()
        {
            PublishCallCount = 0;
            PublishAfterWrite = false;
            LastWriteMarkerSeen = 0;
        }
    }
}
