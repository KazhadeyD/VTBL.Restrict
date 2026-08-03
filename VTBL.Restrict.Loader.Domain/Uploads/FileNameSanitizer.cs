using System;
using System.IO;
using System.Linq;
using System.Text;

namespace VTBL.Restrict.Loader.Domain.Uploads
{
    /// <summary>
    /// Санитизация имени исходного файла для целевого пути на шаре.
    /// Удаляет сегменты пути: .., \, /.
    /// </summary>
    public static class FileNameSanitizer
    {
        /// <summary>
        /// Возвращает безопасное имя файла без path traversal.
        /// </summary>
        public static string Sanitize(string originalFileName)
        {
            if (string.IsNullOrWhiteSpace(originalFileName))
            {
                return string.Empty;
            }

            var name = originalFileName.Replace('\\', '/');
            name = name.Split('/').LastOrDefault() ?? string.Empty;
            name = name.Replace("..", string.Empty, StringComparison.Ordinal);

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (Array.IndexOf(invalid, ch) >= 0 || ch == '/' || ch == '\\')
                {
                    continue;
                }

                builder.Append(ch);
            }

            return builder.ToString().Trim();
        }
    }
}
