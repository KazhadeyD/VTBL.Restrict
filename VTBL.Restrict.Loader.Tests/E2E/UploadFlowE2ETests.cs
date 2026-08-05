using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Tests.Fakes;
using VTBL.Restrict.Loader.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Loader.Tests.E2E
{
    /// <summary>
    /// Полный сценарий загрузки: проверка, запись, уведомление.
    /// </summary>
    public sealed class UploadFlowE2ETests : IClassFixture<RestrictWebAppFactory>, IDisposable
    {
        private readonly RestrictWebAppFactory _factory;

        public UploadFlowE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HappyUpload_NotifyAfterWrite()
        {
            _factory.TrackingNotifier.Reset();
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await PostValidUploadAsync(client);

            Assert.Equal(1, _factory.TrackingNotifier.PublishCallCount);
        }

        [Fact]
        public async Task ShareFail_NoPublish()
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
        public async Task PublishFail_FileRemains()
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
            Assert.Contains("correlationId:", html);

            var match = Regex.Match(html, @"correlationId:\s*([0-9a-fA-F-]{36})");
            Assert.True(match.Success);
        }

        public void Dispose()
        {
            _factory.TryCleanupRemoteRoot();
        }

        private async Task PostValidUploadAsync(HttpClient client)
        {
            var html = await PostUploadAsync(client, new byte[] { 10, 20, 30 }, "list.xlsx");
            Assert.Contains("передан на обработку", html);
            ExtractCorrelationId(html);
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
