using Microsoft.EntityFrameworkCore;
using VTBL.Restrict.Loader.Context.Entities;

namespace VTBL.Restrict.Loader.Context
{
    public sealed class RestrictDbContext : DbContext
    {
        public RestrictDbContext(DbContextOptions<RestrictDbContext> options)
            : base(options)
        {
        }

        public DbSet<ListTypeEntity> ListTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("restrict");

            modelBuilder.Entity<ListTypeEntity>(e =>
            {
                e.ToTable("ListType", "restrict");
                e.HasKey(x => x.ListTypeId);
                e.Property(x => x.Code).HasMaxLength(64).IsRequired();
                e.Property(x => x.Name).HasMaxLength(256).IsRequired();
                e.Property(x => x.FolderSegment).HasMaxLength(128).IsRequired();
                e.Property(x => x.RoutingKeySuffix).HasMaxLength(128).IsRequired();
            });
        }
    }
}
