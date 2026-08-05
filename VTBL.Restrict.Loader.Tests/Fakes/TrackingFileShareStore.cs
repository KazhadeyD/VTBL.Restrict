using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Tests.Fakes
{
    /// <summary>
    /// Подсчёт вызовов WriteAsIs для проверки fail-fast валидации.
    /// </summary>
    public sealed class TrackingFileShareStore : IFileShareStore
    {
        public int WriteCallCount { get; private set; }

        public Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken)
        {
            WriteCallCount++;
            return Task.FromResult(targetFullPath);
        }
    }
}
