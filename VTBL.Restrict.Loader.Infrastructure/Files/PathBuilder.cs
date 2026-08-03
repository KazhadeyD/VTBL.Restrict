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
        /// {RemoteRoot}\{FolderSegment}\{yyyy}\{MM}\{dd}\{correlationIdN}_{sanitizedOriginalName}
        /// </summary>
        public static string BuildTargetPath(
            string remoteRoot,
            string folderSegment,
            DateTime utcNow,
            Guid correlationId,
            string originalFileName)
        {
            if (string.IsNullOrWhiteSpace(remoteRoot))
            {
                throw new ArgumentException("RemoteRoot is required.", nameof(remoteRoot));
            }

            if (string.IsNullOrWhiteSpace(folderSegment))
            {
                throw new ArgumentException("FolderSegment is required.", nameof(folderSegment));
            }

            var sanitized = FileNameSanitizer.Sanitize(originalFileName);
            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "upload";
            }

            var fileName = correlationId.ToString("N") + "_" + sanitized;
            var root = remoteRoot.TrimEnd('\\', '/');
            var segment = folderSegment.Trim('\\', '/');

            return Path.Combine(
                root,
                segment,
                utcNow.ToString("yyyy"),
                utcNow.ToString("MM"),
                utcNow.ToString("dd"),
                fileName);
        }
    }
}
