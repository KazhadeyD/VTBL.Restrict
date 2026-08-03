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
        public DbSet<UploadBatchEntity> UploadBatches { get; set; }
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

            modelBuilder.Entity<UploadBatchEntity>(e =>
            {
                e.ToTable("UploadBatch", "restrict");
                e.HasKey(x => x.UploadBatchId);
                e.Property(x => x.UploadBatchId).HasDefaultValueSql("NEWSEQUENTIALID()");
                e.HasIndex(x => x.CorrelationId).IsUnique();
                e.Property(x => x.OriginalFileName).HasMaxLength(512).IsRequired();
                e.Property(x => x.StoredFilePath).HasMaxLength(1024).IsRequired();
                e.Property(x => x.UploadedBy).HasMaxLength(256);
                e.Property(x => x.NotifyStatus).HasMaxLength(32).IsRequired();
                e.HasOne(x => x.ListType)
                    .WithMany(x => x.UploadBatches)
                    .HasForeignKey(x => x.ListTypeId);
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
