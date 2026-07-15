using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.Tests.Infrastructure;
using Xunit;

namespace VTBL.Restrict.Tests.E2E
{
    /// <summary>
    /// Legacy Error Processing GET (обновлён под InMemory store seed).
    /// </summary>
    public sealed class ErrorProcessingStubE2ETests : IClassFixture<RestrictWebAppFactory>
    {
        private readonly RestrictWebAppFactory _factory;

        public ErrorProcessingStubE2ETests(RestrictWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task TC_E2E_03_Get_ErrorProcessingCase_ReturnsFieldList()
        {
            var caseId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            _factory.ErrorProcessingStore.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                SourceFilePath = @"\\stub\path\file.xlsx",
                Items = new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        FieldCode = "STUB_FIELD",
                        RawValue = "raw",
                        ParserMessage = "stub parser message",
                        IsRequired = true,
                        SortOrder = 0
                    }
                }
            });

            var client = _factory.CreateClient();
            var response = await client.GetAsync($"/error-processing/{caseId}?token=stub");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("STUB_FIELD", html);
            Assert.Contains("MVK", html);
            Assert.DoesNotContain("at VTBL.Restrict", html);
        }
    }
}
