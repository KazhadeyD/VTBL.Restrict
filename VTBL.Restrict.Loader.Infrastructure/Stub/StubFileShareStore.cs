using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;

namespace VTBL.Restrict.Loader.Infrastructure.Stub
{
    /// <summary>
    /// Заглушка файловой шары. Не читает и не интерпретирует содержимое потока (EC-01).
    /// </summary>
    public sealed class StubFileShareStore : IFileShareStore
    {
        /// <inheritdoc />
        public Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken)
        {
            // Заглушка: не трогаем content (не Seek/Read для бизнес-логики), возвращаем целевой путь.
            return Task.FromResult(targetFullPath);
        }
    }
}
