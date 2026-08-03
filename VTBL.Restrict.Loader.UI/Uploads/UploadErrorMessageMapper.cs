using VTBL.Restrict.Loader.Application.Uploads;

namespace VTBL.Restrict.Loader.UI.Uploads
{
    /// <summary>
    /// Маппинг кодов ошибок загрузки в понятные пользователю тексты.
    /// </summary>
    public static class UploadErrorMessageMapper
    {
        public static string Map(string errorCode, string commandMessage)
        {
            if (!string.IsNullOrWhiteSpace(commandMessage) &&
                (errorCode == null ||
                 errorCode == UploadErrorCodes.Validation ||
                 errorCode == UploadErrorCodes.Share ||
                 errorCode == UploadErrorCodes.Rmq))
            {
                return commandMessage.Trim();
            }

            switch (errorCode)
            {
                case UploadErrorCodes.Validation:
                    return "Проверьте тип списка и файл (расширение, размер, непустота).";
                case UploadErrorCodes.Share:
                    return "Не удалось сохранить файл на файловый ресурс. Повторите позже или обратитесь в поддержку.";
                case UploadErrorCodes.Rmq:
                    return "Файл сохранён, но уведомление сервису обработки не отправлено. Обратитесь в поддержку с correlationId.";
                case UploadErrorCodes.Unexpected:
                    return "Произошла непредвиденная ошибка. Обратитесь в поддержку.";
                default:
                    return string.IsNullOrWhiteSpace(commandMessage)
                        ? "Ошибка загрузки. Обратитесь в поддержку."
                        : commandMessage.Trim();
            }
        }

        public static string FriendlyTitle(bool success, string errorCode)
        {
            if (success)
            {
                return "Готово";
            }

            switch (errorCode)
            {
                case UploadErrorCodes.Validation:
                    return "Ошибка проверки";
                case UploadErrorCodes.Share:
                    return "Ошибка файлового ресурса";
                case UploadErrorCodes.Rmq:
                    return "Ошибка уведомления";
                default:
                    return "Ошибка";
            }
        }
    }
}
