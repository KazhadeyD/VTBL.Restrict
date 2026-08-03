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
        public DbSet<RcListEntryEntity> RcListEntries { get; set; }

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

            modelBuilder.Entity<RcListEntryEntity>(e =>
            {
                e.ToTable("RcListEntry", "restrict");
                e.HasKey(x => x.RcListEntryId);
                e.Property(x => x.RcListEntryId).HasDefaultValueSql("NEWSEQUENTIALID()");
                e.HasIndex(x => x.ExternalRecordId).IsUnique();
                e.Property(x => x.BankOfRussiaSigns).HasMaxLength(512).IsRequired();
                e.Property(x => x.Regions).HasMaxLength(512);
                e.Property(x => x.AdditionalInfo).HasMaxLength(256);
                e.Property(x => x.RecordType).HasMaxLength(64).IsRequired();
            });
        }
    }
}
