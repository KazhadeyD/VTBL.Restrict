using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using VTBL.Restrict.Loader.UI.Models;
using VTBL.Restrict.Loader.UI.Pages.Upload;
using Xunit;

namespace VTBL.Restrict.Loader.UI.Tests.Unit
{
    /// <summary>
    /// TC-UNIT (UI): OnPost вызывает UploadRestrictFileCommand ровно один раз.
    /// </summary>
    public sealed class UploadPageModelTests
    {
        [Fact]
        public async Task OnPostAsync_InvokesCommandOnce()
        {
            var counting = new CountingUploadCommand();
            var page = new IndexModel(counting, new InMemoryListTypeReadStore())
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext()
                },
                Input = new UploadFormModel
                {
                    ListTypeCode = "MVK",
                    File = CreateFormFile("a.xlsx", new byte[] { 1 })
                }
            };

            var result = await page.OnPostAsync(CancellationToken.None);

            Assert.IsType<PageResult>(result);
            Assert.Equal(1, counting.CallCount);
            Assert.True(page.IsSuccess);
        }

        private static IFormFile CreateFormFile(string name, byte[] bytes)
        {
            var stream = new System.IO.MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "Input.File", name)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }

        private sealed class CountingUploadCommand : UploadRestrictFileCommand
        {
            public int CallCount { get; private set; }

            public CountingUploadCommand()
            {
            }

            public override Task<UploadRestrictFileResult> ExecuteAsync(
                UploadRestrictFileRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(new UploadRestrictFileResult
                {
                    Success = true,
                    CorrelationId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    Message = "Файл успешно загружен и передан на обработку."
                });
            }
        }
    }
}
