using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.ErrorProcessing;
using VTBL.Restrict.Loader.Domain.Enums;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using VTBL.Restrict.Loader.UI.Pages.ErrorProcessing;
using Xunit;

namespace VTBL.Restrict.Loader.UI.Tests.Unit
{
    /// <summary>
    /// TC-UNIT: MapForm копирует RowNumber и UploadCorrelationId.
    /// </summary>
    public sealed class ErrorProcessingIndexModelTests
    {
        [Fact]
        public async Task OnGetAsync_MapForm_CopiesRowNumberAndUploadCorrelationId()
        {
            var caseId = Guid.NewGuid();
            var correlationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            var itemId = Guid.NewGuid();

            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                SourceFilePath = @"\\share\file.xlsx",
                UploadCorrelationId = correlationId,
                Items = new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = itemId,
                        FieldCode = "F1",
                        RowNumber = 15,
                        RawValue = "raw",
                        ParserMessage = "msg",
                        IsRequired = true,
                        SortOrder = 1
                    }
                }
            });

            var page = new IndexModel(
                new GetErrorProcessingForOperatorQuery(store),
                new ResolveErrorProcessingCommand(store),
                NullLogger<IndexModel>.Instance)
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext()
                },
                CaseId = caseId
            };

            var result = await page.OnGetAsync(CancellationToken.None);

            Assert.IsType<PageResult>(result);
            Assert.Null(page.AccessError);
            Assert.NotNull(page.Form);
            Assert.Equal(correlationId, page.Form.UploadCorrelationId);
            Assert.Single(page.Form.Items);
            Assert.Equal(15, page.Form.Items[0].RowNumber);
            Assert.Equal("F1", page.Form.Items[0].FieldCode);
        }

        [Fact]
        public async Task OnGetAsync_MapForm_NullRowNumberAndCorrelationRemainNull()
        {
            var caseId = Guid.NewGuid();
            var store = new InMemoryErrorProcessingCaseStore();
            store.Seed(new ErrorProcessingCaseRecord
            {
                ErrorProcessingCaseId = caseId,
                ListTypeId = 1,
                ListTypeCode = "MVK",
                ListTypeName = "МВК",
                Status = ErrorProcessingStatus.Pending,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                SourceFilePath = null,
                UploadCorrelationId = null,
                Items = new[]
                {
                    new ErrorProcessingItemRecord
                    {
                        ErrorProcessingItemId = Guid.NewGuid(),
                        FieldCode = "F2",
                        RowNumber = null,
                        RawValue = "r",
                        ParserMessage = "p",
                        IsRequired = false,
                        SortOrder = 0
                    }
                }
            });

            var page = new IndexModel(
                new GetErrorProcessingForOperatorQuery(store),
                new ResolveErrorProcessingCommand(store),
                NullLogger<IndexModel>.Instance)
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext()
                },
                CaseId = caseId
            };

            await page.OnGetAsync(CancellationToken.None);

            Assert.NotNull(page.Form);
            Assert.Null(page.Form.UploadCorrelationId);
            Assert.Null(page.Form.Items[0].RowNumber);
        }
    }
}
