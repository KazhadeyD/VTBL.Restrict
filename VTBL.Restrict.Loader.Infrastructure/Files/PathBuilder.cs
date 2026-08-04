using System;
using System.IO;
using VTBL.Restrict.Loader.Domain.Uploads;

namespace VTBL.Restrict.Loader.Infrastructure.Files
{
    /// <summary>
    /// Правила построения пути выкладки файла.
    /// </summary>
    public static class PathBuilder
    {
        /// <summary>
        /// {RemoteRoot}\{correlationIdN}_{sanitizedOriginalName}
        /// </summary>
        public static string BuildTargetPath(
            string remoteRoot,
            Guid correlationId,
            string originalFileName)
        {
            if (string.IsNullOrWhiteSpace(remoteRoot))
            {
                throw new ArgumentException("RemoteRoot is required.", nameof(remoteRoot));
            }

            var sanitized = FileNameSanitizer.Sanitize(originalFileName);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "upload";
            }

            var fileName = correlationId.ToString("N") + "_" + sanitized;
            var root = remoteRoot.TrimEnd('\\', '/');

            return Path.Combine(
                root,
                fileName);
        }
    }
}
