using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Application.Observability;
using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.UI.Models;
using VTBL.Restrict.Loader.UI.Uploads;

namespace VTBL.Restrict.Loader.UI.Pages.Upload
{
    /// <summary>
    /// Страница загрузки файла. Типы списков — из справочника активных ListType.
    /// </summary>
    [RequestSizeLimit(104_857_600)]
    public class IndexModel : PageModel
    {
        private readonly UploadRestrictFileCommand _uploadCommand;
        private readonly IListTypeReadStore _listTypeReadStore;
        private readonly ILogger _logger;

        public IndexModel(
            UploadRestrictFileCommand uploadCommand,
            IListTypeReadStore listTypeReadStore,
            ILogger<IndexModel> logger = null)
        {
            _uploadCommand = uploadCommand;
            _listTypeReadStore = listTypeReadStore;
            _logger = logger ?? NullLogger<IndexModel>.Instance;
        }

        [BindProperty]
        public UploadFormModel Input { get; set; } = new UploadFormModel();

        public IReadOnlyList<SelectListItem> ListTypeOptions { get; private set; }

        public string ResultMessage { get; private set; }

        public string ResultTitle { get; private set; }

        public bool IsSuccess { get; private set; }

        public string CorrelationIdText { get; private set; }

        public string ErrorCode { get; private set; }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            await PopulateListTypesAsync(cancellationToken);
        }

        /// <summary>
        /// Обрабатывает HTTP POST: валидирует форму, собирает запрос в application-layer и возвращает страницу с результатом.
        /// </summary>
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

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                [OperationLogScope.KeyOperation] = "upload-http",
                ["requestId"] = HttpContext.TraceIdentifier,
                [OperationLogScope.KeyListType] = Input.ListTypeCode ?? string.Empty
            }))
            {
                // Держим всё в одном “пакете”: один HTTP-запрос -> понятные сквозные логи.
                // Благодаря scope по ним потом легко искать проблемный correlationId.
                _logger.LogInformation("Upload HTTP POST received");

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

                _logger.LogInformation(
                    "Upload HTTP POST completed with success={Success} errorCode={ErrorCode}",
                    result.Success,
                    result.ErrorCode ?? string.Empty);

                return Page();
            }
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
        }

        /// <summary>
        /// Загружает список типов для формы оператора.
        /// </summary>
        private async Task PopulateListTypesAsync(CancellationToken cancellationToken)
        {
            var types = await _listTypeReadStore.GetActiveAsync(cancellationToken);
            ListTypeOptions = types
                .Select(t => new SelectListItem(t.Name, t.Code))
                .ToList();
        }
    }
}
