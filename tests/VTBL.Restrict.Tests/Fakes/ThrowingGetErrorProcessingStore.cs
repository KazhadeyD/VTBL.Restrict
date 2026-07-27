using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.UI;

namespace VTBL.Restrict.Tests.Fakes
{
    /// <summary>
    /// IErrorProcessingStore: GetById всегда бросает (Db-failure smoke).
    /// </summary>
    public sealed class ThrowingGetErrorProcessingStore : IErrorProcessingStore
    {
        public Task<ErrorProcessingCaseRecord> GetByIdAsync(
            Guid errorProcessingCaseId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated get db failure");

        public Task<IReadOnlyList<ErrorProcessingCaseSummaryRecord>> ListPendingSummariesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ErrorProcessingCaseSummaryRecord>>(
                Array.Empty<ErrorProcessingCaseSummaryRecord>());

        public Task<bool> ResolveAsync(
            Guid errorProcessingCaseId,
            IReadOnlyDictionary<Guid, string> itemUserValues,
            string resolvedBy,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>
    /// Test host с GetById → exception (UI AccessError / ReasonDb).
    /// </summary>
    public sealed class ThrowingGetErrorProcessingWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:RestrictDb"] = string.Empty,
                    ["RestrictStorage:RemoteRoot"] = System.IO.Path.GetTempPath(),
                    ["RabbitMq:Host"] = string.Empty
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IErrorProcessingStore>();
                services.AddSingleton<IErrorProcessingStore>(new ThrowingGetErrorProcessingStore());
            });
        }
    }

    /// <summary>
    /// IErrorProcessingStore: ListPendingSummaries всегда бросает (Db-failure списка).
    /// </summary>
    public sealed class ThrowingListErrorProcessingStore : IErrorProcessingStore
    {
        public Task<ErrorProcessingCaseRecord> GetByIdAsync(
            Guid errorProcessingCaseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ErrorProcessingCaseRecord>(null);

        public Task<IReadOnlyList<ErrorProcessingCaseSummaryRecord>> ListPendingSummariesAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated list db failure");

        public Task<bool> ResolveAsync(
            Guid errorProcessingCaseId,
            IReadOnlyDictionary<Guid, string> itemUserValues,
            string resolvedBy,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>
    /// Test host с ListPendingSummaries → exception (UI list error alert).
    /// </summary>
    public sealed class ThrowingListErrorProcessingWebAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:RestrictDb"] = string.Empty,
                    ["RestrictStorage:RemoteRoot"] = System.IO.Path.GetTempPath(),
                    ["RabbitMq:Host"] = string.Empty
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IErrorProcessingStore>();
                services.AddSingleton<IErrorProcessingStore>(new ThrowingListErrorProcessingStore());
            });
        }
    }
}
