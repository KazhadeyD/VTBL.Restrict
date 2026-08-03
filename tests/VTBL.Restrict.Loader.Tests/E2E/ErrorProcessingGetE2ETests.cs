using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Loader.Tests.E2E
{
    /// <summary>
    /// TC-E2E (task 3.1): Error Processing open by caseId, no token gate.
    /// </summary>
    public sealed class ErrorProcessingGetE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public ErrorProcessingGetE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_01_PendingCase_ShowsOrderedItems()
        {
            var caseId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.Pending,
                CreateItem(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"), "SECOND", 2),
                CreateItem(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"), "FIRST_FIELD", 1));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var html = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync());

            Assert.Contains("FIRST_FIELD", html);
            Assert.Contains("SECOND", html);
            Assert.Contains("МВК", html);
            Assert.True(html.IndexOf("FIRST_FIELD", StringComparison.Ordinal) <
                        html.IndexOf("SECOND", StringComparison.Ordinal));
            Assert.DoesNotContain("data-case-readonly", html);
            Assert.Contains("Сохранить", html);
        }

        [Fact]
        public async Task TC_E2E_02_UnknownCaseId_ShowsRefusal()
        {
            var client = _factory.CreateClient();
            var html = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{Guid.NewGuid()}")).Content.ReadAsStringAsync());

            Assert.Contains("не найден", html);
            Assert.Contains("data-access-error", html);
            Assert.DoesNotContain("Сохранить", html);
            Assert.DoesNotContain("at VTBL.", html);
        }

        [Fact]
        public async Task TC_E2E_03_TokenQueryDoesNotAffectAccess()
        {
            var caseId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.Pending,
                CreateItem(Guid.NewGuid(), "TOKEN_PROOF", 1));

            var client = _factory.CreateClient();
            var withToken = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}?token=totally-wrong")).Content.ReadAsStringAsync());
            var withoutToken = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}")).Content.ReadAsStringAsync());

            Assert.Contains("TOKEN_PROOF", withToken);
            Assert.Contains("TOKEN_PROOF", withoutToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/error-processing/{caseId}?token=x")).StatusCode);
        }

        [Fact]
        public async Task TC_E2E_04_ResolvedByUser_IsReadOnly()
        {
            var caseId = Guid.NewGuid();
            Seed(caseId, ErrorProcessingStatus.ResolvedByUser,
                CreateItem(Guid.NewGuid(), "DONE_FIELD", 1));

            var client = _factory.CreateClient();
            var html = System.Net.WebUtility.HtmlDecode(
                await (await client.GetAsync($"/error-processing/{caseId}?token=anything")).Content.ReadAsStringAsync());

            Assert.Contains("DONE_FIELD", html);
            Assert.Contains("data-case-readonly", html);
            Assert.Contains("уже обработан", html);
            Assert.DoesNotContain(">Сохранить<", html);
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

        private static ErrorProcessingItemRecord CreateItem(Guid id, string field, int sort)
        {
            return new ErrorProcessingItemRecord
            {
                ErrorProcessingItemId = id,
                FieldCode = field,
                RawValue = "raw",
                ParserMessage = "parser",
                UserValue = null,
                IsRequired = true,
                SortOrder = sort
            };
        }
    }
}
