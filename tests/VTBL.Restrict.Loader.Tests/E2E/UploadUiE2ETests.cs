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
    /// TC-E2E (task 2.5): Upload UI — controls, validation UX, retry on RMQ fail.
    /// </summary>
    public sealed class UploadUiE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public UploadUiE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_PageContainsSelectFileInputSubmitAndDnD()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var html = await (await client.GetAsync("/Upload")).Content.ReadAsStringAsync();
            var decoded = System.Net.WebUtility.HtmlDecode(html);

            Assert.Contains("data-list-type-select", decoded);
            Assert.Contains("data-upload-file", decoded);
            Assert.Contains("data-upload-submit", decoded);
            Assert.Contains("data-upload-dropzone", decoded);
            Assert.Contains("data-upload-browse", decoded);
            Assert.Contains("data-upload-file-clear", decoded);
            Assert.Contains("Отправить", decoded);
            Assert.Contains("Выбрать файл", decoded);
            Assert.Contains("Удалить файл", decoded);
            Assert.Contains("upload-dnd.js", decoded);
            Assert.Contains("value=\"MVK\"", decoded);
            Assert.Contains("value=\"TERRORISTS\"", decoded);
        }

        [Fact]
        public async Task TC_E2E_02_ValidationError_DistinctText_NoStack()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var get = await client.GetAsync("/Upload");
            var token = ExtractToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent(""), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(new byte[] { 1 }), "Input.File", "list.xlsx");

            var post = await client.PostAsync("/Upload", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            Assert.Contains("data-error-code=\"Validation\"", html);
            Assert.Contains("Ошибка проверки", html);
            Assert.DoesNotContain("at VTBL.", html);
            Assert.DoesNotContain("Exception", html);
            Assert.DoesNotContain("StackTrace", html);
        }

        [Fact]
        public async Task TC_E2E_03_RmqFail_ShowsRetryControl()
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

            var get = await client.GetAsync("/Upload");
            var token = ExtractToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Input.File", "list.xlsx");

            var post = await client.PostAsync("/Upload", content);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            Assert.Contains("data-error-code=\"Rmq\"", html);
            Assert.Contains("data-retry-button", html);
            Assert.Contains("Повторить уведомление", html);
            Assert.Contains("correlationId:", html);
            Assert.DoesNotContain("at VTBL.", html);
        }

        private static string ExtractToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success);
            return match.Groups[1].Value;
        }
    }
}
