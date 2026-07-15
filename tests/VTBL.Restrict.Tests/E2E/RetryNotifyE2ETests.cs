using System;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Tests.Fakes;
using VTBL.Restrict.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Tests.E2E
{
    /// <summary>
    /// TC-E2E (task 2.4): Retry notify без повторной выкладки файла.
    /// </summary>
    public sealed class RetryNotifyE2ETests : IClassFixture<RestrictWebAppFactory>, IDisposable
    {
        private readonly RestrictWebAppFactory _factory;

        public RetryNotifyE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_FailedBatch_RetrySuccess_NoFileShareWrite()
        {
            _factory.TrackingNotifier.Reset();
            var correlationId = Guid.NewGuid();
            await SeedBatchAsync(correlationId, NotifyStatus.Failed);

            var trackingShare = new TrackingFileShareStore();
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IFileShareStore>();
                    services.AddSingleton<IFileShareStore>(trackingShare);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var html = await PostRetryAsync(client, correlationId);
            Assert.Contains("повторно отправлено", html);

            var batch = await _factory.UploadBatchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None);
            Assert.Equal(NotifyStatus.Published, batch.NotifyStatus);
            Assert.Equal(1, _factory.TrackingNotifier.PublishCallCount);
            Assert.Equal(0, trackingShare.WriteCallCount);
        }

        [Fact]
        public async Task TC_E2E_02_PublishedBatch_RetryRejected_NoPublish()
        {
            _factory.TrackingNotifier.Reset();
            var correlationId = Guid.NewGuid();
            await SeedBatchAsync(correlationId, NotifyStatus.Published);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var html = await PostRetryAsync(client, correlationId);

            Assert.Contains("уже отправлено", html);
            Assert.Equal(0, _factory.TrackingNotifier.PublishCallCount);
            Assert.Equal(NotifyStatus.Published, (await _factory.UploadBatchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None)).NotifyStatus);
        }

        [Fact]
        public async Task TC_E2E_03_UnknownCorrelation_Error()
        {
            _factory.TrackingNotifier.Reset();
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var html = await PostRetryAsync(client, Guid.NewGuid());

            Assert.Contains("не найдена", html);
            Assert.Equal(0, _factory.TrackingNotifier.PublishCallCount);
        }

        public void Dispose()
        {
            _factory.TryCleanupRemoteRoot();
        }

        private async Task SeedBatchAsync(Guid correlationId, NotifyStatus status)
        {
            await _factory.UploadBatchStore.InsertPendingAsync(new UploadBatchRecord
            {
                CorrelationId = correlationId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                OriginalFileName = "list.xlsx",
                StoredFilePath = @"C:\inbox\mvk\seed.xlsx",
                UploadedBy = "e2e",
                UploadedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            }, CancellationToken.None);

            if (status != NotifyStatus.Pending)
            {
                await _factory.UploadBatchStore.UpdateNotifyStatusAsync(correlationId, status, CancellationToken.None);
            }
        }

        private static async Task<string> PostRetryAsync(HttpClient client, Guid correlationId)
        {
            var get = await client.GetAsync("/Upload");
            var token = ExtractRequestVerificationToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent(correlationId.ToString("D")), "correlationId");

            var post = await client.PostAsync("/Upload/Retry", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            return System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
        }

        private static string ExtractRequestVerificationToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Antiforgery token not found");
            return match.Groups[1].Value;
        }
    }
}
