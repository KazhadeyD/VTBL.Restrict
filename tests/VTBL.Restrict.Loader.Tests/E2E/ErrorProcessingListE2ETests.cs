using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Tests.Fakes;
using VTBL.Restrict.Loader.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Loader.Tests.E2E
{
    /// <summary>
    /// E2E usable UI списка Error Processing (in-process TestServer).
    /// Live SQL — EP-4.1.
    /// </summary>
    public sealed class ErrorProcessingListE2ETests
    {
        /// <summary>
        /// TC-E2E-01: Seed 2 Pending + 1 Resolved → 2 строки; Resolved отсутствует; порядок CreatedAt DESC.
        /// </summary>
        [Fact]
        public async Task TC_E2E_01_ListRoute_WithPendingAndResolved_ShowsOnlyPendingOrderedDesc()
        {
            using var factory = new RestrictWebAppFactory();
            var older = Guid.NewGuid();
            var newer = Guid.NewGuid();
            var resolved = Guid.NewGuid();

            factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = older,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\older.xlsx",
                Items = Array.Empty<ErrorProcessingItemRecord>()
            });
            factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = newer,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\newer.xlsx",
                Items = Array.Empty<ErrorProcessingItemRecord>()
            });
            factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = resolved,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.ResolvedByUser,
                CreatedAtUtc = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\resolved.xlsx",
                Items = Array.Empty<ErrorProcessingItemRecord>()
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/error-processing");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("data-ep-list=\"true\"", html);
            Assert.Contains($"data-ep-case-id=\"{newer}\"", html);
            Assert.Contains($"data-ep-case-id=\"{older}\"", html);
            Assert.DoesNotContain($"data-ep-case-id=\"{resolved}\"", html);
            Assert.DoesNotContain(resolved.ToString(), html);

            var rowMatches = Regex.Matches(html, @"data-ep-case-id=""");
            Assert.Equal(2, rowMatches.Count);

            Assert.True(
                html.IndexOf($"data-ep-case-id=\"{newer}\"", StringComparison.Ordinal) <
                html.IndexOf($"data-ep-case-id=\"{older}\"", StringComparison.Ordinal));

            Assert.Contains("data-ep-list-type", html);
            Assert.Contains("data-ep-list-status", html);
            Assert.Contains("data-ep-list-created", html);
            Assert.Contains("data-ep-list-expires", html);
            Assert.Contains("data-ep-list-source", html);
            Assert.Contains(@"\\share\newer.xlsx", html);
            Assert.DoesNotContain("data-ep-list-empty", html);
            Assert.DoesNotContain("data-ep-list-error", html);
            Assert.DoesNotContain("at VTBL.", html);

            factory.TryCleanupRemoteRoot();
        }

        /// <summary>
        /// TC-E2E-02: Пустой store → empty state, HTTP 200.
        /// </summary>
        [Fact]
        public async Task TC_E2E_02_ListRoute_EmptyStore_ReturnsEmptyState()
        {
            using var factory = new RestrictWebAppFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/error-processing");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("data-ep-list-empty", html);
            Assert.Contains("Нет кейсов для обработки", html);
            Assert.DoesNotContain("data-ep-list=\"true\"", html);
            Assert.DoesNotContain("data-ep-list-error", html);
            Assert.DoesNotContain("at VTBL.", html);
            factory.TryCleanupRemoteRoot();
        }

        /// <summary>
        /// TC-E2E-03: href из списка → /error-processing/{guid} и GET открывает кейс.
        /// </summary>
        [Fact]
        public async Task TC_E2E_03_ListLink_OpensCaseByDeepLink()
        {
            using var factory = new RestrictWebAppFactory();
            var caseId = Guid.NewGuid();
            factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\from-list.xlsx",
                Items = new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = Guid.NewGuid(),
                        FieldCode = "LIST_PROOF",
                        RawValue = "raw",
                        ParserMessage = "parser",
                        IsRequired = true,
                        SortOrder = 1,
                        RowNumber = 3
                    }
                }
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var listResponse = await client.GetAsync("/error-processing");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var listHtml = WebUtility.HtmlDecode(await listResponse.Content.ReadAsStringAsync());
            var expectedHref = $"/error-processing/{caseId}";
            Assert.Contains($"href=\"{expectedHref}\"", listHtml);
            Assert.Contains("data-ep-case-link=\"true\"", listHtml);

            var caseResponse = await client.GetAsync(expectedHref);
            Assert.Equal(HttpStatusCode.OK, caseResponse.StatusCode);

            var caseHtml = WebUtility.HtmlDecode(await caseResponse.Content.ReadAsStringAsync());
            Assert.Contains("LIST_PROOF", caseHtml);
            Assert.Contains("Сохранить", caseHtml);
            Assert.DoesNotContain("data-access-error", caseHtml);

            factory.TryCleanupRemoteRoot();
        }

        /// <summary>
        /// List Db-failure → error alert, без stack (регресс UI error-path).
        /// </summary>
        [Fact]
        public async Task TC_E2E_04_ListStoreThrows_ShowsErrorAlertWithoutStack()
        {
            using var factory = new ThrowingListErrorProcessingWebAppFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/error-processing");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("data-ep-list-error", html);
            Assert.Contains("Не удалось загрузить список кейсов.", html);
            Assert.DoesNotContain("data-ep-list=\"true\"", html);
            Assert.DoesNotContain("data-ep-list-empty", html);
            Assert.DoesNotContain("at VTBL.", html);
            Assert.DoesNotContain("InvalidOperationException", html);
            Assert.DoesNotContain("simulated list db failure", html);
        }

        /// <summary>
        /// TC-E2E-04 (resolve): deep-link Pending → POST resolve → ResolvedByUser (регресс).
        /// </summary>
        [Fact]
        public async Task TC_E2E_04_DeepLinkResolve_PendingBecomesResolvedByUser()
        {
            using var factory = new RestrictWebAppFactory();
            var caseId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\resolve-from-deeplink.xlsx",
                Items = new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = itemId,
                        FieldCode = "NAME",
                        RawValue = "raw",
                        ParserMessage = "parser",
                        IsRequired = true,
                        SortOrder = 1
                    }
                }
            });

            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var getHtml = await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync();
            var tokenMatch = Regex.Match(getHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(tokenMatch.Success, "Antiforgery token not found");

            using var content = new System.Net.Http.MultipartFormDataContent();
            content.Add(new System.Net.Http.StringContent(tokenMatch.Groups[1].Value), "__RequestVerificationToken");
            content.Add(new System.Net.Http.StringContent(caseId.ToString()), "CaseId");
            content.Add(new System.Net.Http.StringContent(caseId.ToString()), "Form.ErrorProcessingCaseId");
            content.Add(new System.Net.Http.StringContent("MVK"), "Form.ListTypeCode");
            content.Add(new System.Net.Http.StringContent("МВК"), "Form.ListTypeName");
            content.Add(new System.Net.Http.StringContent("Pending"), "Form.Status");
            content.Add(new System.Net.Http.StringContent(DateTime.UtcNow.AddDays(1).ToString("o")), "Form.ExpiresAtUtc");
            content.Add(new System.Net.Http.StringContent(@"\\share\resolve-from-deeplink.xlsx"), "Form.SourceFilePath");
            content.Add(new System.Net.Http.StringContent("false"), "Form.IsReadOnly");
            content.Add(new System.Net.Http.StringContent(itemId.ToString()), "Form.Items[0].ErrorProcessingItemId");
            content.Add(new System.Net.Http.StringContent("NAME"), "Form.Items[0].FieldCode");
            content.Add(new System.Net.Http.StringContent("raw"), "Form.Items[0].RawValue");
            content.Add(new System.Net.Http.StringContent("parser"), "Form.Items[0].ParserMessage");
            content.Add(new System.Net.Http.StringContent("true"), "Form.Items[0].IsRequired");
            content.Add(new System.Net.Http.StringContent("1"), "Form.Items[0].SortOrder");
            content.Add(new System.Net.Http.StringContent("fixed-from-list"), "Form.Items[0].UserValue");

            var post = await client.PostAsync($"/error-processing/{caseId}", content);
            Assert.Equal(HttpStatusCode.OK, post.StatusCode);
            var postHtml = WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
            Assert.Contains("data-case-status=\"ResolvedByUser\"", postHtml);
            Assert.Contains("fixed-from-list", postHtml);

            var listHtml = WebUtility.HtmlDecode(
                await (await client.GetAsync("/error-processing")).Content.ReadAsStringAsync());
            Assert.DoesNotContain($"data-ep-case-id=\"{caseId}\"", listHtml);

            var stored = await factory.ErrorProcessingStore.GetByIdAsync(caseId, default);
            Assert.Equal(ErrorProcessingStatus.ResolvedByUser, stored.Status);
            Assert.Equal("fixed-from-list", stored.Items[0].UserValue);

            factory.TryCleanupRemoteRoot();
        }

        /// <summary>
        /// GET /error-processing/{pendingCaseId} → форма кейса (регресс deep-link без списка).
        /// </summary>
        [Fact]
        public async Task DeepLinkCase_StillReturnsOk()
        {
            using var factory = new RestrictWebAppFactory();
            var caseId = Guid.NewGuid();
            factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow.AddHours(-3),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
                SourceFilePath = @"\\share\inbox\mvk\file.xlsx",
                Items = new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = Guid.NewGuid(),
                        FieldCode = "LIST_PROOF",
                        RawValue = "raw",
                        ParserMessage = "parser",
                        IsRequired = true,
                        SortOrder = 1,
                        RowNumber = 7
                    }
                }
            });

            var client = factory.CreateClient();
            var response = await client.GetAsync($"/error-processing/{caseId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("LIST_PROOF", html);
            Assert.Contains("data-ep-row-number=\"7\"", html);
            Assert.Contains("информационно, не блокирует", html);
            Assert.Contains("data-ep-back-to-list", html);
            Assert.Contains("Сохранить", html);

            factory.TryCleanupRemoteRoot();
        }

        /// <summary>
        /// GET /Upload → 200 + navbar «Загрузка» / «Обработка ошибок» (регресс).
        /// </summary>
        [Fact]
        public async Task UploadEntrypoint_ReturnsOk_AndNavbarLinksToList()
        {
            using var factory = new RestrictWebAppFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var response = await client.GetAsync("/Upload");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Обработка ошибок", html);
            Assert.Contains("Загрузка", html);
            Assert.Contains("href=\"/error-processing\"", html);

            factory.TryCleanupRemoteRoot();
        }

        /// <summary>
        /// GetById throws → AccessError без stack (регресс deep-link Db-failure).
        /// </summary>
        [Fact]
        public async Task GetByIdThrows_ShowsAccessErrorWithoutStack()
        {
            using var factory = new ThrowingGetErrorProcessingWebAppFactory();
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var caseId = Guid.NewGuid();
            var response = await client.GetAsync($"/error-processing/{caseId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("data-access-error", html);
            Assert.Contains("Не удалось загрузить данные кейса.", html);
            Assert.DoesNotContain("at VTBL.", html);
            Assert.DoesNotContain("InvalidOperationException", html);
            Assert.DoesNotContain("Сохранить", html);
        }
    }
}
