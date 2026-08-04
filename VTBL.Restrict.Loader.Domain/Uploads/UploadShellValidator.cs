using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VTBL.Restrict.Loader.Domain.Uploads
{
    /// <summary>
    /// Проверка метаданных файла (тип, расширение, размер). Содержимое потока не читается.
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
}
