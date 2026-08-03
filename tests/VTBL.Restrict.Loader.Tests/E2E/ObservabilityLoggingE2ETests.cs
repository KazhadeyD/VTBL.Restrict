using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Tests.Fakes;
using VTBL.Restrict.Loader.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Loader.Tests.E2E
{
    /// <summary>
    /// TC-E2E (task 4.1): Upload fail пишет лог с correlationId.
    /// </summary>
    public sealed class ObservabilityLoggingE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public ObservabilityLoggingE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_UploadRmqFail_WritesLogWithCorrelationId()
        {
            var logProvider = new CollectingLoggerProvider();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IUploadNotifier>();
                    services.AddSingleton<IUploadNotifier>(new FailingUploadNotifier());
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(logProvider);
                        logging.SetMinimumLevel(LogLevel.Information);
                    });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var get = await client.GetAsync("/Upload");
            var token = ExtractToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Input.File", "list.xlsx");

            var post = await client.PostAsync("/Upload", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            var correlationMatch = Regex.Match(html, @"correlationId:\s*([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
            Assert.True(correlationMatch.Success, "correlationId expected in UI after partial RMQ fail");
            var correlationId = correlationMatch.Groups[1].Value;

            var joined = string.Join('\n', logProvider.Entries.Select(e => e.Message));
            Assert.Contains(correlationId, joined, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                joined.Contains("Upload publish failed", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("errorCode=Rmq", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains(UploadErrorCodes.Rmq, StringComparison.Ordinal),
                "Expected upload/publish fail log. Logs:\n" + joined);
        }

        private static string ExtractToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success);
            return match.Groups[1].Value;
        }
    }
}
