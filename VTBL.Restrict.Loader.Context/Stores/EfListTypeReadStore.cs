using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VTBL.Restrict.Loader.Application.Abstractions;
using VTBL.Restrict.Loader.Domain.ListTypes;

namespace VTBL.Restrict.Loader.Context.Stores
{
    public sealed class EfListTypeReadStore : IListTypeReadStore
    {
        private readonly RestrictDbContext _db;

        public EfListTypeReadStore(RestrictDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<ListTypeInfo>> GetActiveAsync(CancellationToken cancellationToken)
        {
            var rows = await _db.ListTypes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

            return rows.Select(Map).ToList();
        }

        public async Task<ListTypeInfo> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var entity = await _db.ListTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == code && x.IsActive, cancellationToken);

            return entity == null ? null : Map(entity);
        }

        private static ListTypeInfo Map(Entities.ListTypeEntity x)
        {
            return new ListTypeInfo
            {
                Code = x.Code,
                Name = x.Name,
                RemoteRoot = null
            };
        }
    }
}
