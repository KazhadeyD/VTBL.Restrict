using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using VTBL.Restrict.Loader.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Loader.Tests.E2E
{
    /// <summary>
    /// Happy-path Upload после 2.2: реальная запись + correlationId в ответе.
    /// </summary>
    public sealed class UploadStubE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly HttpClient _client;

        public UploadStubE2ETests(RestrictWebAppFactory factory)
        {
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task TC_E2E_01_Get_Upload_PageLoads()
        {
            var response = await _client.GetAsync("/Upload");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Загрузка рестриктивного списка", html);
            Assert.Contains("Отправить", html);
            Assert.Contains("value=\"MVK\"", System.Net.WebUtility.HtmlDecode(html));
        }

        [Fact]
        public async Task TC_E2E_02_Post_Upload_ReturnsSuccessMessage()
        {
            var get = await _client.GetAsync("/Upload");
            var getHtml = await get.Content.ReadAsStringAsync();
            var token = ExtractRequestVerificationToken(getHtml);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Input.File", "list.xlsx");

            var post = await _client.PostAsync("/Upload", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
            Assert.Contains("передан на обработку", html);
            Assert.Matches(@"correlationId:\s*[0-9a-fA-F-]{36}", html);
        }

        private static string ExtractRequestVerificationToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Antiforgery token not found");
            return match.Groups[1].Value;
        }
    }
}
