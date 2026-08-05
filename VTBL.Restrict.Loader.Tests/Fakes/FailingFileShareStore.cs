using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Tests.Fakes
{
    /// <summary>
    /// Симуляция сбоя записи на шару (mid-write / IO error).
    /// </summary>
    public sealed class FailingFileShareStore : IFileShareStore
    {
        public Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken)
        {
            var tempPath = targetFullPath + ".tmp";
            var directory = Path.GetDirectoryName(tempPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(tempPath, "partial");
            File.Delete(tempPath);
            throw new IOException("Simulated file share write failure.");
        }
    }
}
