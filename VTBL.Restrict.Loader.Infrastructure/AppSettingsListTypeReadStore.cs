using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.ListTypes;

namespace VTBL.Restrict.Loader.Infrastructure
{
    /// <summary>
    /// Источник ListTypes из конфигурации (appsettings.json): ключ "ListTypes" (массив элементов).
    /// БД игнорируется.
    /// </summary>
    public sealed class AppSettingsListTypeReadStore : IListTypeReadStore
    {
        private readonly IReadOnlyList<ListTypeInfo> _items;

        public AppSettingsListTypeReadStore(IEnumerable<ListTypeInfo> items)
        {
            _items = (items ?? Array.Empty<ListTypeInfo>())
                .Select(Clone)
                .ToList();
        }

        public Task<IReadOnlyList<ListTypeInfo>> GetActiveAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ListTypeInfo> active = _items
                .Select(Clone)
                .OrderBy(x => x.Name)
                .ToList();

            return Task.FromResult(active);
        }

        public Task<ListTypeInfo> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return Task.FromResult<ListTypeInfo>(null);
            }

            var found = _items.FirstOrDefault(x =>
                string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(found == null ? null : Clone(found));
        }

        private static ListTypeInfo Clone(ListTypeInfo source)
        {
            if (source == null)
            {
                return null;
            }

            return new ListTypeInfo
            {
                Code = source.Code,
                Name = source.Name,
                RemoteRoot = source.RemoteRoot
            };
        }
    }
}

