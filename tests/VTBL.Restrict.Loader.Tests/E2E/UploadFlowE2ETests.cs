using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using VTBL.Restrict.Loader.Tests.Fakes;
using VTBL.Restrict.Loader.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Loader.Tests.E2E
{
    /// <summary>
    /// Полный сценарий загрузки: проверка, запись, учёт, уведомление.
    /// </summary>
    public sealed class UploadFlowE2ETests : IClassFixture<RestrictWebAppFactory>, IDisposable
    {
        private readonly RestrictWebAppFactory _factory;

        public UploadFlowE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_HappyUpload_BatchPublished_NotifyAfterWrite()
        {
            _factory.TrackingNotifier.Reset();
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var correlationId = await PostValidUploadAsync(client);

            var batch = await _factory.UploadBatchStore.GetByCorrelationIdAsync(correlationId, CancellationToken.None);
            Assert.NotNull(batch);
            Assert.Equal(NotifyStatus.Published, batch.NotifyStatus);
            Assert.Equal(1, _factory.TrackingNotifier.PublishCallCount);
        }

        [Fact]
        public async Task TC_E2E_02_ShareFail_NoBatchNoPublish()
        {
            _factory.TrackingNotifier.Reset();
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IFileShareStore>();
                    services.AddSingleton<IFileShareStore>(new FailingFileShareStore());
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var html = await PostUploadAsync(client, new byte[] { 1, 2, 3 }, "list.xlsx");
            Assert.Contains("Не удалось сохранить файл", html);
            Assert.Equal(0, _factory.TrackingNotifier.PublishCallCount);
        }

        [Fact]
        public async Task TC_E2E_03_PublishFail_BatchFailed_FileRemains()
        {
            _factory.TrackingNotifier.Reset();
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IUploadNotifier>();
                    services.AddSingleton<IUploadNotifier>(new FailingUploadNotifier());
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var html = await PostUploadAsync(client, new byte[] { 10, 20, 30 }, "list.xlsx");
            Assert.Contains("уведомление не отправлено", html);

            var batch = _factory.UploadBatchStore.GetLatestForTest();
            Assert.NotNull(batch);
            Assert.Equal(NotifyStatus.Failed, batch.NotifyStatus);
            Assert.True(File.Exists(batch.StoredFilePath));
        }

        public void Dispose()
        {
            _factory.TryCleanupRemoteRoot();
        }

        private async Task<Guid> PostValidUploadAsync(HttpClient client)
        {
            var html = await PostUploadAsync(client, new byte[] { 10, 20, 30 }, "list.xlsx");
            Assert.Contains("передан на обработку", html);
            return ExtractCorrelationId(html);
        }

        private static async Task<string> PostUploadAsync(HttpClient client, byte[] payload, string fileName)
        {
            var get = await client.GetAsync("/Upload");
            var token = ExtractRequestVerificationToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(payload), "Input.File", fileName);

            var post = await client.PostAsync("/Upload", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            return System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
        }

        private static Guid ExtractCorrelationId(string html)
        {
            var match = Regex.Match(html, @"correlationId:\s*([0-9a-fA-F-]{36})");
            Assert.True(match.Success, "correlationId not found");
            return Guid.Parse(match.Groups[1].Value);
        }

        private static string ExtractRequestVerificationToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Antiforgery token not found");
            return match.Groups[1].Value;
        }
    }
}
