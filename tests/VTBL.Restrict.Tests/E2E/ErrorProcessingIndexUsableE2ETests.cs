using System;
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
    /// Usable UI кейса Index: Pending/empty/Resolved/отказы/ExpiresAt info.
    /// </summary>
    public sealed class ErrorProcessingIndexUsableE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public ErrorProcessingIndexUsableE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_PendingWithItems_ShowsUsableFields()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.Pending, DateTime.UtcNow.AddDays(2),
                CreateItem(itemId, "NAME_FIELD", required: true, rowNumber: 42));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var html = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync());

            Assert.Contains("data-ep-case=\"true\"", html);
            Assert.Contains("data-ep-field-code=\"NAME_FIELD\"", html);
            Assert.Contains("data-ep-raw-value=\"true\"", html);
            Assert.Contains("data-ep-parser-message=\"true\"", html);
            Assert.Contains("data-user-value-input=\"true\"", html);
            Assert.Contains("data-ep-required=\"true\"", html);
            Assert.Contains("data-ep-row-number=\"42\"", html);
            Assert.Contains("data-ep-save=\"true\"", html);
            Assert.Contains("информационно, не блокирует", html);
            Assert.Contains("data-ep-back-to-list", html);
            Assert.DoesNotContain("data-ep-placeholder", html);
            Assert.DoesNotContain("at VTBL.", html);
        }

        [Fact]
        public async Task TC_E2E_02_PendingWithoutItems_EmptyStateThenResolve()
        {
            var caseId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.Pending, DateTime.UtcNow.AddDays(1));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var getHtml = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync());

            Assert.Contains("data-ep-items-empty=\"true\"", getHtml);
            Assert.Contains("Нет полей для обработки", getHtml);
            Assert.Contains("data-ep-save=\"true\"", getHtml);
            Assert.DoesNotContain("data-ep-items=\"true\"", getHtml);

            var token = ExtractAntiforgery(getHtml);
            using var content = BuildEmptyItemsPost(caseId, token);
            var post = await client.PostAsync($"/error-processing/{caseId}", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);

            var postHtml = System.Net.WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
            Assert.Contains("data-case-status=\"ResolvedByUser\"", postHtml);
            Assert.Contains("data-case-readonly", postHtml);
            Assert.Contains("уже обработан", postHtml);
            Assert.DoesNotContain("data-ep-save", postHtml);

            var stored = await _factory.ErrorProcessingStore.GetByIdAsync(caseId, default);
            Assert.Equal(ErrorProcessingStatus.ResolvedByUser, stored.Status);
        }

        [Fact]
        public async Task TC_E2E_03_ResolvedByUser_ReadOnly_NoSuccessfulSaveViaUi()
        {
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var item = CreateItem(itemId, "DONE_FIELD", required: true, rowNumber: 1);
            item.UserValue = "already";
            Seed(caseId, ErrorProcessingStatus.ResolvedByUser, DateTime.UtcNow.AddDays(1), item);

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var getHtml = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync());

            Assert.Contains("data-case-readonly", getHtml);
            Assert.Contains("уже обработан", getHtml);
            Assert.DoesNotContain("data-ep-save", getHtml);
            Assert.DoesNotContain("data-user-value-input", getHtml);
            Assert.Contains("data-user-value=\"true\"", getHtml);

            var token = ExtractAntiforgery(getHtml);
            using var content = BuildItemPost(caseId, token, itemId, "hack");
            var postHtml = System.Net.WebUtility.HtmlDecode(
                await (await client.PostAsync($"/error-processing/{caseId}", content)).Content.ReadAsStringAsync());

            Assert.True(
                postHtml.Contains("data-error-code=\"Conflict\"", StringComparison.OrdinalIgnoreCase) ||
                postHtml.Contains("уже обработан", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("data-resolve-result=\"success\"", postHtml);
            Assert.DoesNotContain("at VTBL.", postHtml);

            var stored = await _factory.ErrorProcessingStore.GetByIdAsync(caseId, default);
            Assert.Equal("already", stored.Items[0].UserValue);
        }

        [Fact]
        public async Task TC_E2E_04_UnknownAndUnavailable_ShowRefusalWithoutStack()
        {
            var client = _factory.CreateClient();

            var unknown = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{Guid.NewGuid()}")).Content.ReadAsStringAsync());
            Assert.Contains("не найден", unknown);
            Assert.Contains("data-access-error", unknown);
            Assert.DoesNotContain("Сохранить", unknown);
            Assert.DoesNotContain("at VTBL.", unknown);
            Assert.Contains("data-ep-back-to-list", unknown);

            var expiredId = Guid.NewGuid();
            Seed(expiredId, ErrorProcessingStatus.Expired, DateTime.UtcNow.AddDays(-1));
            var expired = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{expiredId}")).Content.ReadAsStringAsync());
            Assert.Contains("недоступен", expired);
            Assert.Contains("data-access-error", expired);
            Assert.DoesNotContain("Сохранить", expired);
            Assert.DoesNotContain("at VTBL.", expired);

            var cancelledId = Guid.NewGuid();
            Seed(cancelledId, ErrorProcessingStatus.Cancelled, DateTime.UtcNow.AddDays(1));
            var cancelled = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{cancelledId}")).Content.ReadAsStringAsync());
            Assert.Contains("недоступен", cancelled);
            Assert.Contains("data-access-error", cancelled);
            Assert.DoesNotContain("at VTBL.", cancelled);
        }

        [Fact]
        public async Task TC_E2E_05_PastExpiresAt_PendingStillEditable()
        {
            var caseId = Guid.NewGuid();
            var pastExpires = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            Seed(caseId, ErrorProcessingStatus.Pending, pastExpires,
                CreateItem(Guid.NewGuid(), "STILL_EDIT", required: false, rowNumber: null));

            var client = _factory.CreateClient();
            var html = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync());

            Assert.Contains("data-ep-expires-info=\"true\"", html);
            Assert.Contains("информационно, не блокирует", html);
            Assert.Contains("2020-01-01", html);
            Assert.Contains("data-case-status=\"Pending\"", html);
            Assert.Contains("data-ep-save=\"true\"", html);
            Assert.Contains("data-user-value-input=\"true\"", html);
            Assert.DoesNotContain("data-case-readonly", html);
            Assert.DoesNotContain("data-access-error", html);
        }

        private void Seed(
            Guid caseId,
            ErrorProcessingStatus status,
            DateTime expiresAtUtc,
            params ErrorProcessingItemRecord[] items)
        {
            _factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = status,
                ExpiresAtUtc = expiresAtUtc,
                SourceFilePath = @"\\share\inbox\mvk\file.xlsx",
                UploadCorrelationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Items = items ?? Array.Empty<ErrorProcessingItemRecord>()
            });
        }

        private static ErrorProcessingItemRecord CreateItem(
            Guid id,
            string field,
            bool required,
            int? rowNumber)
        {
            return new ErrorProcessingItemRecord
            {
                ErrorProcessingItemId = id,
                FieldCode = field,
                RowNumber = rowNumber,
                RawValue = "raw-value",
                ParserMessage = "parser-message",
                UserValue = null,
                IsRequired = required,
                SortOrder = 1
            };
        }

        private static MultipartFormDataContent BuildEmptyItemsPost(Guid caseId, string antiforgery)
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
            return content;
        }

        private static MultipartFormDataContent BuildItemPost(
            Guid caseId,
            string antiforgery,
            Guid itemId,
            string userValue)
        {
            var content = BuildEmptyItemsPost(caseId, antiforgery);
            content.Add(new StringContent(itemId.ToString()), "Form.Items[0].ErrorProcessingItemId");
            content.Add(new StringContent("DONE_FIELD"), "Form.Items[0].FieldCode");
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
