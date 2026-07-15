using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Application.Abstractions;
using VTBL.Restrict.Domain.ListTypes;

namespace VTBL.Restrict.Infrastructure.Stub
{
    /// <summary>
    /// In-memory ListType store для dev/test без SQL (seed MVP + контроль IsActive).
    /// </summary>
    public sealed class InMemoryListTypeReadStore : IListTypeReadStore
    {
        private readonly List<ListTypeInfo> _items;

        /// <summary>
        /// Seed по умолчанию (MVK/TERRORISTS active, OTHER inactive).
        /// </summary>
        public InMemoryListTypeReadStore()
            : this(CreateDefaultSeed())
        {
        }

        /// <summary>
        /// Кастомный seed для unit-тестов. Не регистрировать этот ctor в DI напрямую
        /// (DI подставит пустой <see cref="IEnumerable{T}"/>).
        /// </summary>
        public InMemoryListTypeReadStore(IEnumerable<ListTypeInfo> seed)
        {
            _items = (seed ?? Array.Empty<ListTypeInfo>()).Select(Clone).ToList();
        }

        public Task<IReadOnlyList<ListTypeInfo>> GetActiveAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ListTypeInfo> active = _items
                .Where(x => x.IsActive)
                .Select(Clone)
                .OrderBy(x => x.Name)
                .ToList();
            return Task.FromResult(active);
        }

        public Task<ListTypeInfo> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            var found = _items.FirstOrDefault(x =>
                x.IsActive &&
                string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found == null ? null : Clone(found));
        }

        public Task<ListTypeInfo> GetByIdAsync(int listTypeId, CancellationToken cancellationToken)
        {
            var found = _items.FirstOrDefault(x => x.ListTypeId == listTypeId);
            return Task.FromResult(found == null ? null : Clone(found));
        }

        private static IEnumerable<ListTypeInfo> CreateDefaultSeed()
        {
            yield return new ListTypeInfo
            {
                ListTypeId = 1,
                Code = "MVK",
                Name = "МВК",
                FolderSegment = "mvk",
                RoutingKeySuffix = "mvk",
                IsActive = true
            };
            yield return new ListTypeInfo
            {
                ListTypeId = 2,
                Code = "TERRORISTS",
                Name = "Террористы",
                FolderSegment = "terrorists",
                RoutingKeySuffix = "terrorists",
                IsActive = true
            };
            yield return new ListTypeInfo
            {
                ListTypeId = 3,
                Code = "OTHER",
                Name = "Прочее",
                FolderSegment = "other",
                RoutingKeySuffix = "other",
                IsActive = false
            };
        }

        private static ListTypeInfo Clone(ListTypeInfo source)
        {
            return new ListTypeInfo
            {
                ListTypeId = source.ListTypeId,
                Code = source.Code,
                Name = source.Name,
                FolderSegment = source.FolderSegment,
                RoutingKeySuffix = source.RoutingKeySuffix,
                IsActive = source.IsActive
            };
        }
    }
}
