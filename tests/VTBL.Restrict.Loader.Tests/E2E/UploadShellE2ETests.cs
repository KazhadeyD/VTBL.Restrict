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
    /// Страница загрузки и валидация без вызова файловой шары.
    /// ListType через InMemory seed (MVK/TERRORISTS/NFA) при пустом RestrictDb.
    /// </summary>
    public sealed class UploadShellE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public UploadShellE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_Get_Upload_ShowsActiveListTypes()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/Upload");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var decoded = System.Net.WebUtility.HtmlDecode(html);
            Assert.Contains("value=\"MVK\"", decoded);
            Assert.Contains("value=\"TERRORISTS\"", decoded);
            Assert.Contains("value=\"NFA\"", decoded);
            Assert.Contains("МВК", decoded);
            Assert.Contains("Террористы", decoded);
            Assert.Contains("Нелегальная финансовая деятельность", decoded);
            Assert.DoesNotContain("value=\"OTHER\"", decoded);
        }

        [Fact]
        public async Task TC_E2E_02_Post_BadExtension_Validation_DoesNotCallFileShare()
        {
            var tracking = new TrackingFileShareStore();
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IFileShareStore>();
                    services.AddSingleton<IFileShareStore>(tracking);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var get = await client.GetAsync("/Upload");
            var token = ExtractRequestVerificationToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Input.File", "malware.exe");

            var post = await client.PostAsync("/Upload", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
            Assert.Contains("Недопустимое расширение", html);
            Assert.Equal(0, tracking.WriteCallCount);
        }

        private static string ExtractRequestVerificationToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Antiforgery token not found");
            return match.Groups[1].Value;
        }
    }
}
