using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VTBL.Restrict.Loader.Application.Abstractions
{
    /// <summary>
    /// Выкладка файла без изменений на удалённую папку. Содержимое не интерпретировать.
    /// </summary>
    public interface IFileShareStore
    {
        /// <summary>
        /// Побайтово копирует поток в целевой путь (temp + rename). Не читать содержимое для бизнес-логики.
        /// </summary>
        Task<string> WriteAsIsAsync(Stream content, string targetFullPath, CancellationToken cancellationToken);
    }
}
