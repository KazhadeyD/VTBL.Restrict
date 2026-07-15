using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VTBL.Restrict.Application.Uploads;

namespace VTBL.Restrict.UI.Pages.Upload
{
    /// <summary>
    /// Повтор уведомления RMQ по correlationId без повторной выкладки файла.
    /// </summary>
    public class RetryModel : PageModel
    {
        private readonly RetryUploadNotificationCommand _retryCommand;

        public RetryModel(RetryUploadNotificationCommand retryCommand)
        {
            _retryCommand = retryCommand;
        }

        public string ResultMessage { get; private set; }

        public bool IsSuccess { get; private set; }

        public string CorrelationIdText { get; private set; }

        public void OnGet()
        {
            ResultMessage = "Укажите correlationId через форму на странице загрузки.";
            IsSuccess = false;
        }

        public async Task<IActionResult> OnPostAsync(Guid correlationId, CancellationToken cancellationToken)
        {
            if (correlationId == Guid.Empty)
            {
                IsSuccess = false;
                ResultMessage = "Некорректный correlationId.";
                return Page();
            }

            CorrelationIdText = correlationId.ToString("D");

            var result = await _retryCommand.ExecuteAsync(correlationId, cancellationToken);
            IsSuccess = result.Success;
            ResultMessage = MapMessage(result);

            return Page();
        }

        private static string MapMessage(RetryUploadNotificationResult result)
        {
            if (result == null)
            {
                return "Ошибка повторного уведомления.";
            }

            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                return result.Message;
            }

            if (result.Success)
            {
                return "Уведомление отправлено.";
            }

            switch (result.ErrorCode)
            {
                case UploadErrorCodes.NotFound:
                    return "Загрузка с указанным correlationId не найдена.";
                case UploadErrorCodes.Conflict:
                    return "Уведомление уже отправлено. Повторная публикация запрещена.";
                case UploadErrorCodes.Rmq:
                    return "Не удалось отправить уведомление. Повторите позже.";
                default:
                    return "Ошибка повторного уведомления.";
            }
        }
    }
}
