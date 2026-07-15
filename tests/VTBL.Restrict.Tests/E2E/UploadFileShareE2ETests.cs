using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Infrastructure.Files;
using VTBL.Restrict.Tests.Fakes;
using VTBL.Restrict.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Tests.E2E
{
    /// <summary>
    /// TC-E2E (task 2.2): реальная запись as-is в temp RemoteRoot.
    /// </summary>
    public sealed class UploadFileShareE2ETests : IClassFixture<RestrictWebAppFactory>, IDisposable
    {
        private readonly RestrictWebAppFactory _factory;
        private readonly string _remoteRoot;

        public UploadFileShareE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
            _remoteRoot = factory.TestRemoteRoot;
        }

        [Fact]
        public async Task TC_E2E_01_ValidUpload_WritesFileAsIs()
        {
            var payload = new byte[] { 10, 20, 30, 40, 50 };
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var get = await client.GetAsync("/Upload");
            var token = ExtractRequestVerificationToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(payload), "Input.File", "list.xlsx");

            var post = await client.PostAsync("/Upload", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);

            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
            Assert.Contains("передан на обработку", html);

            var correlationMatch = Regex.Match(html, @"correlationId:\s*([0-9a-fA-F-]{36})");
            Assert.True(correlationMatch.Success, "correlationId not found in response");
            var correlationId = Guid.Parse(correlationMatch.Groups[1].Value);

            var found = FindUploadedFile(_remoteRoot, "mvk", correlationId, "list.xlsx");
            Assert.NotNull(found);
            Assert.Equal(payload, await File.ReadAllBytesAsync(found));
            Assert.False(File.Exists(found + ".tmp"));
        }

        [Fact]
        public async Task TC_E2E_02_MidWriteFailure_NoFinalFile()
        {
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IFileShareStore>();
                    services.AddSingleton<IFileShareStore>(new FailingFileShareStore());
                });
            }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var get = await client.GetAsync("/Upload");
            var token = ExtractRequestVerificationToken(await get.Content.ReadAsStringAsync());

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent("MVK"), "Input.ListTypeCode");
            content.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Input.File", "list.xlsx");

            var post = await client.PostAsync("/Upload", content);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
            Assert.Contains("Не удалось сохранить файл", html);

            var files = Directory.Exists(_remoteRoot)
                ? Directory.GetFiles(_remoteRoot, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Assert.Empty(files);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_remoteRoot))
                {
                    Directory.Delete(_remoteRoot, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp test root.
            }
        }

        private static string FindUploadedFile(string remoteRoot, string segment, Guid correlationId, string originalName)
        {
            if (!Directory.Exists(remoteRoot))
            {
                return null;
            }

            var suffix = correlationId.ToString("N") + "_" + originalName;
            foreach (var file in Directory.GetFiles(remoteRoot, "*" + suffix, SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar) ||
                    file.Contains(Path.AltDirectorySeparatorChar + segment + Path.AltDirectorySeparatorChar))
                {
                    return file;
                }
            }

            return null;
        }

        private static string ExtractRequestVerificationToken(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Antiforgery token not found");
            return match.Groups[1].Value;
        }
    }
}
