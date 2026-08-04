using System;

namespace VTBL.Restrict.Loader.Application.Abstractions
{
    /// <summary>
    /// Построение целевого пути файла на шаре.
    /// </summary>
    public interface IUploadPathBuilder
    {
        /// <summary>
        /// {RemoteRoot}\{correlationId}_{sanitizedOriginalName}
        /// </summary>
        string BuildTargetPath(
            string remoteRoot,
            Guid correlationId,
            string originalFileName);
    }
}
