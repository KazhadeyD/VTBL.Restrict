using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Tests.E2E
{
    /// <summary>
    /// TC-E2E (task 3.2): Resolve Error Processing by caseId.
    /// </summary>
    public sealed class ErrorProcessingResolveE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public ErrorProcessingResolveE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_ResolveHappy_StatusResolved_UserValuesSaved()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.Pending, CreateItem(itemId, "NAME", required: true));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var get = await client.GetAsync($"/error-processing/{caseId}");
            var getHtml = await get.Content.ReadAsStringAsync();
            var token = ExtractAntiforgery(getHtml);

            using var content = BuildPost(caseId, token, itemId, "corrected-value");
            var post = await client.PostAsync($"/error-processing/{caseId}", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            Assert.Contains("data-resolve-result=\"success\"", html);
            Assert.Contains("сохранены", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data-case-status=\"ResolvedByUser\"", html);
            Assert.Contains("corrected-value", html);
            Assert.Contains("data-case-readonly", html);

            var stored = await _factory.ErrorProcessingStore.GetByIdAsync(caseId, default);
            Assert.Equal(ErrorProcessingStatus.ResolvedByUser, stored.Status);
            Assert.Equal("corrected-value", stored.Items[0].UserValue);
        }

        [Fact]
        public async Task TC_E2E_02_MissingRequired_Validation_StaysPending()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.Pending, CreateItem(itemId, "REQ", required: true));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var getHtml = await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync();
            var token = ExtractAntiforgery(getHtml);

            using var content = BuildPost(caseId, token, itemId, "");
            var post = await client.PostAsync($"/error-processing/{caseId}", content);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            Assert.Contains("data-error-code=\"Validation\"", html);
            Assert.Contains("обязательн", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("data-case-status=\"Pending\"", html);
            Assert.Contains("data-ep-save", html);
            Assert.DoesNotContain("at VTBL.", html);

            var stored = await _factory.ErrorProcessingStore.GetByIdAsync(caseId, default);
            Assert.Equal(ErrorProcessingStatus.Pending, stored.Status);
        }

        [Fact]
        public async Task TC_E2E_03_ResolveAlreadyResolved_Forbidden()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var item = CreateItem(itemId, "DONE", required: true);
            item.UserValue = "already";
            Seed(caseId, ErrorProcessingStatus.ResolvedByUser, item);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var getHtml = await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync();
            var token = ExtractAntiforgery(getHtml);

            using var content = BuildPost(caseId, token, itemId, "hack");
            var post = await client.PostAsync($"/error-processing/{caseId}", content);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            Assert.Contains("data-error-code=\"Conflict\"", html);
            Assert.Contains("уже обработан", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("at VTBL.", html);

            var stored = await _factory.ErrorProcessingStore.GetByIdAsync(caseId, default);
            Assert.Equal("already", stored.Items[0].UserValue);
        }

        [Fact]
        public async Task TC_E2E_04_UnknownCaseId_Post_RefusalNoStack()
        {
            var caseId = Guid.NewGuid();
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var getHtml = await (await client.GetAsync($"/error-processing/{Guid.NewGuid()}")).Content.ReadAsStringAsync();
            // Unknown GET has no form — post to unknown with minimal antiforgery from another seeded page
            var seedId = Guid.NewGuid();
            Seed(seedId, ErrorProcessingStatus.Pending, CreateItem(Guid.NewGuid(), "X", required: false));
            getHtml = await (await client.GetAsync($"/error-processing/{seedId}")).Content.ReadAsStringAsync();
            var token = ExtractAntiforgery(getHtml);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(token), "__RequestVerificationToken");
            content.Add(new StringContent(caseId.ToString()), "CaseId");
            content.Add(new StringContent(caseId.ToString()), "Form.ErrorProcessingCaseId");
            content.Add(new StringContent(Guid.NewGuid().ToString()), "Form.Items[0].ErrorProcessingItemId");
            content.Add(new StringContent("v"), "Form.Items[0].UserValue");

            var post = await client.PostAsync($"/error-processing/{caseId}", content);
            var html = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            Assert.True(
                html.Contains("не найден", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("data-error-code=\"NotFound\"", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("data-access-error", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("at VTBL.", html);
            Assert.DoesNotContain("StackTrace", html);
        }

        private void Seed(Guid caseId, ErrorProcessingStatus status, params ErrorProcessingItemRecord[] items)
        {
            _factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = status,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\inbox\mvk\file.xlsx",
                Items = items
            });
        }

        private static ErrorProcessingItemRecord CreateItem(Guid id, string field, bool required)
        {
            return new ErrorProcessingItemRecord
            {
                ErrorProcessingItemId = id,
                FieldCode = field,
                RawValue = "raw",
                ParserMessage = "parser",
                UserValue = null,
                IsRequired = required,
                SortOrder = 1
            };
        }

        private static MultipartFormDataContent BuildPost(Guid caseId, string antiforgery, Guid itemId, string userValue)
        {
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(antiforgery), "__RequestVerificationToken");
            content.Add(new StringContent(caseId.ToString()), "CaseId");
            content.Add(new StringContent(caseId.ToString()), "Form.ErrorProcessingCaseId");
            content.Add(new StringContent("MVK"), "Form.ListTypeCode");
            content.Add(new StringContent("МВК"), "Form.ListTypeName");
            content.Add(new StringContent("Pending"), "Form.Status");
            content.Add(new StringContent(DateTime.UtcNow.AddDays(1).ToString("o")), "Form.ExpiresAtUtc");
            content.Add(new StringContent(@"\\share\x.xlsx"), "Form.SourceFilePath");
            content.Add(new StringContent("false"), "Form.IsReadOnly");
            content.Add(new StringContent(itemId.ToString()), "Form.Items[0].ErrorProcessingItemId");
            content.Add(new StringContent("NAME"), "Form.Items[0].FieldCode");
            content.Add(new StringContent("raw"), "Form.Items[0].RawValue");
            content.Add(new StringContent("parser"), "Form.Items[0].ParserMessage");
            content.Add(new StringContent("true"), "Form.Items[0].IsRequired");
            content.Add(new StringContent("1"), "Form.Items[0].SortOrder");
            content.Add(new StringContent(userValue ?? string.Empty), "Form.Items[0].UserValue");
            return content;
        }

        private static string ExtractAntiforgery(string html)
        {
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Antiforgery token not found");
            return match.Groups[1].Value;
        }
    }
}
