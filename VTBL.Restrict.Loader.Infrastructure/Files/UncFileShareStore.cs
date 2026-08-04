using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Infrastructure.Files
{
    /// <summary>
    /// Выкладка файла без изменений на UNC/локальный RemoteRoot: temp + rename.
    /// Содержимое потока не интерпретируется.
    /// </summary>
    public sealed class UncFileShareStore : IFileShareStore
    {
        private const int BufferSize = 81920;
        private readonly ILogger _logger;

        public UncFileShareStore(ILogger<UncFileShareStore> logger = null)
        {
            _logger = logger ?? NullLogger<UncFileShareStore>.Instance;
        }

        /// <inheritdoc />
        public async Task<string> WriteAsIsAsync(
            Stream content,
            string targetFullPath,
            CancellationToken cancellationToken)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (string.IsNullOrWhiteSpace(targetFullPath))
            {
                throw new ArgumentException("Target path is required.", nameof(targetFullPath));
            }

            _logger.LogInformation("File share write started for {TargetPath}", targetFullPath);

            var directory = Path.GetDirectoryName(targetFullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = targetFullPath + ".tmp";

            try
            {
                await using (var destination = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    BufferSize,
                    useAsync: true))
                {
                    await content.CopyToAsync(destination, BufferSize, cancellationToken).ConfigureAwait(false);
                }

                if (File.Exists(targetFullPath))
                {
                    File.Delete(targetFullPath);
                }

                File.Move(tempPath, targetFullPath);
                _logger.LogInformation("File share write succeeded for {TargetPath}", targetFullPath);
                return targetFullPath;
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempPath);
                _logger.LogError(ex, "File share write failed for {TargetPath}", targetFullPath);
                throw;
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup temp artifact.
            }
        }
    }
}
