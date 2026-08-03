using System;

namespace VTBL.Restrict.Loader.Application.Abstractions
{
    /// <summary>
    /// Построение целевого пути файла на шаре.
    /// </summary>
    public interface IUploadPathBuilder
    {
        /// <summary>
        /// {RemoteRoot}\{FolderSegment}\{yyyy}\{MM}\{dd}\{correlationId}_{sanitizedOriginalName}
        /// </summary>
        string BuildTargetPath(
            string remoteRoot,
            string folderSegment,
            DateTime utcNow,
            Guid correlationId,
            string originalFileName);
    }
}
