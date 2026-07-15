using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VTBL.Restrict.Domain.Uploads
{
    /// <summary>
    /// Проверка оболочки файла (EC-02). Содержимое потока не читается.
    /// </summary>
    public static class UploadShellValidator
    {
        /// <summary>
        /// Валидирует метаданные загрузки без открытия содержимого файла.
        /// </summary>
        public static ShellValidationResult Validate(
            string listTypeCode,
            string fileName,
            long contentLength,
            IEnumerable<string> allowedExtensions,
            long maxBytes)
        {
            if (string.IsNullOrWhiteSpace(listTypeCode))
            {
                return ShellValidationResult.Fail("Validation", "Не выбран тип списка.");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return ShellValidationResult.Fail("Validation", "Не указано имя файла.");
            }

            if (contentLength <= 0)
            {
                return ShellValidationResult.Fail("Validation", "Файл пустой.");
            }

            if (maxBytes > 0 && contentLength > maxBytes)
            {
                return ShellValidationResult.Fail("Validation", "Размер файла превышает допустимый лимит.");
            }

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                return ShellValidationResult.Fail("Validation", "Недопустимое расширение файла.");
            }

            var normalizedAllowed = (allowedExtensions ?? Array.Empty<string>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(NormalizeExtension)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalizedAllowed.Count == 0 || !normalizedAllowed.Contains(NormalizeExtension(extension)))
            {
                return ShellValidationResult.Fail("Validation", "Недопустимое расширение файла.");
            }

            return ShellValidationResult.Ok();
        }

        private static string NormalizeExtension(string extension)
        {
            var e = extension.Trim();
            if (!e.StartsWith(".", StringComparison.Ordinal))
            {
                e = "." + e;
            }

            return e.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Результат проверки оболочки.
    /// </summary>
    public sealed class ShellValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorCode { get; private set; }
        public string Message { get; private set; }

        public static ShellValidationResult Ok()
        {
            return new ShellValidationResult { IsValid = true };
        }

        public static ShellValidationResult Fail(string errorCode, string message)
        {
            return new ShellValidationResult
            {
                IsValid = false,
                ErrorCode = errorCode,
                Message = message
            };
        }
    }
}
