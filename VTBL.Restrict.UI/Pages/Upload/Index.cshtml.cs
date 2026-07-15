using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Application.Uploads;
using VTBL.Restrict.UI.Models;
using VTBL.Restrict.UI.Uploads;

namespace VTBL.Restrict.UI.Pages.Upload
{
    /// <summary>
    /// Страница загрузки файла. Типы списков — из IListTypeReadStore (UC-05).
    /// </summary>
    [RequestSizeLimit(52_428_800)]
    public class IndexModel : PageModel
    {
        private readonly UploadRestrictFileCommand _uploadCommand;
        private readonly IListTypeReadStore _listTypeReadStore;

        public IndexModel(
            UploadRestrictFileCommand uploadCommand,
            IListTypeReadStore listTypeReadStore)
        {
            _uploadCommand = uploadCommand;
            _listTypeReadStore = listTypeReadStore;
        }

        [BindProperty]
        public UploadFormModel Input { get; set; } = new UploadFormModel();

        public IReadOnlyList<SelectListItem> ListTypeOptions { get; private set; }

        public string ResultMessage { get; private set; }

        public string ResultTitle { get; private set; }

        public bool IsSuccess { get; private set; }

        public string CorrelationIdText { get; private set; }

        public string ErrorCode { get; private set; }

        public bool ShowRetry { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await PopulateListTypesAsync(cancellationToken);
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            await PopulateListTypesAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(Input?.ListTypeCode))
            {
                ModelState.AddModelError("Input.ListTypeCode", "Выберите тип списка");
            }

            if (Input?.File == null || Input.File.Length <= 0)
            {
                ModelState.AddModelError("Input.File", "Выберите файл");
            }

            if (!ModelState.IsValid)
            {
                ApplyClientValidationResult();
                return Page();
            }

            await using var stream = Input.File.OpenReadStream();
            var request = new UploadRestrictFileRequest
            {
                ListTypeCode = Input.ListTypeCode,
                OriginalFileName = Input.File.FileName,
                ContentLength = Input.File.Length,
                Content = stream,
                UploadedBy = User?.Identity?.Name
            };

            var result = await _uploadCommand.ExecuteAsync(request, cancellationToken);
            ApplyCommandResult(result);

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, ResultMessage);
            }

            return Page();
        }

        private void ApplyClientValidationResult()
        {
            IsSuccess = false;
            ErrorCode = UploadErrorCodes.Validation;
            ResultTitle = UploadErrorMessageMapper.FriendlyTitle(false, ErrorCode);
            ResultMessage = UploadErrorMessageMapper.Map(
                UploadErrorCodes.Validation,
                "Проверьте введённые данные: укажите тип списка и файл.");
            CorrelationIdText = null;
            ShowRetry = false;
        }

        private void ApplyCommandResult(UploadRestrictFileResult result)
        {
            IsSuccess = result.Success;
            ErrorCode = result.ErrorCode;
            CorrelationIdText = result.CorrelationId?.ToString("D");
            ResultTitle = UploadErrorMessageMapper.FriendlyTitle(result.Success, result.ErrorCode);
            ResultMessage = result.Success
                ? (result.Message ?? "Файл успешно загружен и передан на обработку.")
                : UploadErrorMessageMapper.Map(result.ErrorCode, result.Message);
            ShowRetry = UploadErrorMessageMapper.ShouldShowRetry(
                result.Success,
                result.ErrorCode,
                CorrelationIdText);
        }

        private async Task PopulateListTypesAsync(CancellationToken cancellationToken)
        {
            var types = await _listTypeReadStore.GetActiveAsync(cancellationToken);
            ListTypeOptions = types
                .Select(t => new SelectListItem(t.Name, t.Code))
                .ToList();
        }
    }
}
