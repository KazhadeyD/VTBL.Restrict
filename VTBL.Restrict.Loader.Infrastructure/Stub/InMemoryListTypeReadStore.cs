using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.ListTypes;

namespace VTBL.Restrict.Loader.Infrastructure.Stub
{
    /// <summary>
    /// In-memory ListType store для dev/test без SQL.
    /// </summary>
    public sealed class InMemoryListTypeReadStore : IListTypeReadStore
    {
        private readonly List<ListTypeInfo> _items;

        /// <summary>
        /// Seed по умолчанию.
        /// </summary>
        public InMemoryListTypeReadStore()
            : this(CreateDefaultSeed(CreateDefaultRemoteRoot()))
        {
        }

        /// <summary>
        /// Seed по умолчанию, но с заданным RemoteRoot для каждого listType.
        /// Удобно для unit/e2e тестов, чтобы ожидаемые пути совпадали.
        /// </summary>
        public InMemoryListTypeReadStore(string remoteRoot)
            : this(CreateDefaultSeed(remoteRoot))
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
                .Select(Clone)
                .OrderBy(x => x.Name)
                .ToList();
            return Task.FromResult(active);
        }

        public Task<ListTypeInfo> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            var found = _items.FirstOrDefault(x =>
                string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found == null ? null : Clone(found));
        }

        private static string CreateDefaultRemoteRoot()
        {
            // Даем non-empty значение, чтобы PathBuilder не падал при использовании дефолтного seed в тестах.
            return System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "VTBL.Restrict.Loader",
                "InMemoryRemoteRoot");
        }

        private static IEnumerable<ListTypeInfo> CreateDefaultSeed(string remoteRoot)
        {
            yield return new ListTypeInfo
            {
                Code = "MVK",
                Name = "МВК",
                RemoteRoot = remoteRoot
            };
            yield return new ListTypeInfo
            {
                Code = "TERRORISTS",
                Name = "Террористы",
                RemoteRoot = remoteRoot
            };
            yield return new ListTypeInfo
            {
                Code = "NFA",
                Name = "Нелегальная финансовая деятельность",
                RemoteRoot = remoteRoot
            };
            yield return new ListTypeInfo
            {
                Code = "OTHER",
                Name = "Прочее",
                RemoteRoot = remoteRoot
            };
        }

        private static ListTypeInfo Clone(ListTypeInfo source)
        {
            return new ListTypeInfo
            {
                Code = source.Code,
                Name = source.Name,
                RemoteRoot = source.RemoteRoot
            };
        }
    }
}
