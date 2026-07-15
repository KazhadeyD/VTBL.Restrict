using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Domain.ListTypes;

namespace VTBL.Restrict.Application.Abstractions
{
    /// <summary>
    /// Чтение справочника типов списков (только активные для UI загрузки).
    /// </summary>
    public interface IListTypeReadStore
    {
        Task<IReadOnlyList<ListTypeInfo>> GetActiveAsync(CancellationToken cancellationToken);
        Task<ListTypeInfo> GetByCodeAsync(string code, CancellationToken cancellationToken);

        /// <summary>
        /// Загрузка типа по Id (в т.ч. для Retry — без фильтра IsActive).
        /// </summary>
        Task<ListTypeInfo> GetByIdAsync(int listTypeId, CancellationToken cancellationToken);
    }
}
