using System;
using VTBL.Restrict.Application.Abstractions;

namespace VTBL.Restrict.Infrastructure.Files
{
    /// <summary>
    /// Реализация <see cref="IUploadPathBuilder"/> для DI.
    /// </summary>
    public sealed class UploadPathBuilder : IUploadPathBuilder
    {
        /// <inheritdoc />
        public string BuildTargetPath(
            string remoteRoot,
            string folderSegment,
            DateTime utcNow,
            Guid correlationId,
            string originalFileName)
        {
            return PathBuilder.BuildTargetPath(
                remoteRoot,
                folderSegment,
                utcNow,
                correlationId,
                originalFileName);
        }
    }
}
