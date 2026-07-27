using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VTBL.Restrict.Application.ErrorProcessing;
using VTBL.Restrict.Application.Observability;
using VTBL.Restrict.Domain.Enums;
using VTBL.Restrict.UI.Models;

namespace VTBL.Restrict.UI.Pages.ErrorProcessing
{
    /// <summary>
    /// Страница обработки ошибок: GET по caseId (token игнорируется), POST resolve.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly GetErrorProcessingForOperatorQuery _getQuery;
        private readonly ResolveErrorProcessingCommand _resolveCommand;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            GetErrorProcessingForOperatorQuery getQuery,
            ResolveErrorProcessingCommand resolveCommand,
            ILogger<IndexModel> logger)
        {
            _getQuery = getQuery;
            _resolveCommand = resolveCommand;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public Guid CaseId { get; set; }

        /// <summary>
        /// Опциональный query-параметр «на будущее»; не валидируется и не логируется целиком.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string Token { get; set; }

        [BindProperty]
        public ErrorProcessingFormModel Form { get; set; }

        public string AccessError { get; private set; }

        public string InfoMessage { get; private set; }

        public string ResultMessage { get; private set; }

        public string ResultErrorCode { get; private set; }

        public bool IsSuccess { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var access = await _getQuery.ExecuteAsync(CaseId, Token, cancellationToken);
            return ApplyAccess(access);
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        {
            if (Form == null)
            {
                AccessError = MapAccessError(ErrorProcessingCaseAccessResult.ReasonNotFound);
                _logger.LogWarning(
                    "ErrorProcessing UI POST without form caseId={CaseId} tokenPresent={TokenPresent}",
                    CaseId,
                    SensitiveLog.DescribeTokenPresence(Token));
                return Page();
            }

            var itemValues = Form.Items?
                .ToDictionary(i => i.ErrorProcessingItemId, i => i.UserValue ?? string.Empty)
                ?? new Dictionary<Guid, string>();

            var result = await _resolveCommand.ExecuteAsync(new ResolveErrorProcessingRequest
            {
                ErrorProcessingCaseId = CaseId,
                RawToken = Token,
                ItemUserValues = itemValues,
                ResolvedBy = User?.Identity?.Name
            }, cancellationToken);

            IsSuccess = result.Success;
            ResultMessage = result.Message;
            ResultErrorCode = result.ErrorCode;

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "Ошибка сохранения");
                if (result.FieldErrors != null)
                {
                    foreach (var pair in result.FieldErrors)
                    {
                        var index = Form.Items?.FindIndex(i => i.ErrorProcessingItemId == pair.Key) ?? -1;
                        if (index >= 0)
                        {
                            ModelState.AddModelError($"Form.Items[{index}].UserValue", pair.Value);
                        }
                    }
                }

                if (result.ErrorCode == ErrorProcessingErrorCodes.Validation ||
                    result.ErrorCode == ErrorProcessingErrorCodes.Db)
                {
                    AccessError = null;
                    Form.IsReadOnly = false;
                    return Page();
                }
            }

            var access = await _getQuery.ExecuteAsync(CaseId, Token, cancellationToken);
            return ApplyAccess(access, preserveResult: true);
        }

        private IActionResult ApplyAccess(ErrorProcessingCaseAccessResult access, bool preserveResult = false)
        {
            if (!access.Succeeded || access.View == null)
            {
                AccessError = MapAccessError(access.FailureReason);
                Form = null;
                _logger.LogWarning(
                    "ErrorProcessing UI access denied caseId={CaseId} reason={Reason} tokenPresent={TokenPresent}",
                    CaseId,
                    access.FailureReason,
                    SensitiveLog.DescribeTokenPresence(Token));
                if (!preserveResult)
                {
                    ResultMessage = null;
                    ResultErrorCode = null;
                    IsSuccess = false;
                }

                return Page();
            }

            AccessError = null;
            Form = MapForm(access.View, Token);
            if (access.View.Status == ErrorProcessingStatus.ResolvedByUser)
            {
                InfoMessage = "Кейс уже обработан. Редактирование недоступно.";
                Form.IsReadOnly = true;
            }

            return Page();
        }

        private static string MapAccessError(string failureReason)
        {
            if (failureReason == ErrorProcessingCaseAccessResult.ReasonUnavailable)
            {
                return "Кейс недоступен для обработки (истёк или отменён).";
            }

            if (failureReason == ErrorProcessingCaseAccessResult.ReasonDb)
            {
                return "Не удалось загрузить данные кейса.";
            }

            return "Ссылка недействительна или кейс не найден.";
        }

        private static ErrorProcessingFormModel MapForm(ErrorProcessingViewModel view, string token)
        {
            return new ErrorProcessingFormModel
            {
                ErrorProcessingCaseId = view.ErrorProcessingCaseId,
                Token = token,
                ListTypeCode = view.ListTypeCode,
                ListTypeName = view.ListTypeName,
                Status = view.Status.ToString(),
                ExpiresAtUtc = view.ExpiresAtUtc,
                SourceFilePath = view.SourceFilePath,
                UploadCorrelationId = view.UploadCorrelationId,
                IsReadOnly = view.IsReadOnly,
                Items = view.Items?.Select(i => new ErrorProcessingItemFormModel
                {
                    ErrorProcessingItemId = i.ErrorProcessingItemId,
                    FieldCode = i.FieldCode,
                    RowNumber = i.RowNumber,
                    RawValue = i.RawValue,
                    ParserMessage = i.ParserMessage,
                    UserValue = i.UserValue,
                    IsRequired = i.IsRequired,
                    SortOrder = i.SortOrder
                }).OrderBy(i => i.SortOrder).ToList() ?? new List<ErrorProcessingItemFormModel>()
            };
        }
    }
}
