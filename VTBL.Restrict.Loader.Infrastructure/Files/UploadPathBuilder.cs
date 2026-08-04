using System;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Infrastructure.Files
{
    /// <summary>
    /// Реализация <see cref="IUploadPathBuilder"/> для DI.
    /// </summary>
    public sealed class UploadPathBuilder : IUploadPathBuilder
    {
        /// <inheritdoc />
        public string BuildTargetPath(
            string remoteRoot,
            Guid correlationId,
            string originalFileName)
        {
            return PathBuilder.BuildTargetPath(
                remoteRoot,
                correlationId,
                originalFileName);
        }
    }
}
