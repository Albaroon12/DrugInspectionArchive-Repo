
using DrugInspectionArchive.Entities;
using Microsoft.EntityFrameworkCore;
using MaterialEntity = DrugInspectionArchive.Entities.Material;

namespace DrugInspectionArchive.Data;

public sealed class ArchiveDbContext : DbContext
{
    public ArchiveDbContext(
        DbContextOptions<ArchiveDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<InspectionBook> InspectionBooks
        => Set<InspectionBook>();

    // استخدام الاسم المستعار لحل تعارض Material
    public DbSet<MaterialEntity> Materials
        => Set<MaterialEntity>();

    public DbSet<BookMaterial> BookMaterials
        => Set<BookMaterial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================
        // Companies
        // =====================================

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.NormalizedName)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(x => x.NormalizedName)
                .IsUnique();

            entity.HasMany(x => x.Books)
                .WithOne(x => x.Company)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================
        // InspectionBooks
        // =====================================

        modelBuilder.Entity<InspectionBook>(entity =>
        {
            entity.ToTable("InspectionBooks");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.BookNumber)
                .HasMaxLength(100);

            entity.Property(x => x.NormalizedBookNumber)
                .HasMaxLength(100);

            entity.Property(x => x.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.PdfRelativePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.FileSha256)
                .HasMaxLength(64);

            entity.Property(x => x.OcrStatus)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.CompanyId,
                x.IsDeleted,
                x.BookDate
            });

            entity.HasIndex(x => new
            {
                x.CompanyId,
                x.NormalizedBookNumber
            });

            entity.HasIndex(x => x.FileSha256);

            entity.HasMany(x => x.BookMaterials)
                .WithOne(x => x.InspectionBook)
                .HasForeignKey(x => x.InspectionBookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================
        // Materials
        // =====================================

        modelBuilder.Entity<MaterialEntity>(entity =>
        {
            entity.ToTable("Materials");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.CanonicalName)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.NormalizedName)
                .IsRequired()
                .HasMaxLength(250);

            entity.HasIndex(x => x.NormalizedName)
                .IsUnique();

            entity.HasMany(x => x.BookMaterials)
                .WithOne(x => x.Material)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================
        // BookMaterials
        // =====================================

        modelBuilder.Entity<BookMaterial>(entity =>
        {
            entity.ToTable("BookMaterials");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.OriginalMaterialName)
                .HasMaxLength(250);

            entity.Property(x => x.BatchNumber)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(x => x.NormalizedBatchNumber)
                .IsRequired()
                .HasMaxLength(120);

            entity.HasIndex(x => x.NormalizedBatchNumber);

            // المادة نفسها يمكن أن تظهر بعدة Batch داخل الكتاب
            // لكن لا نكرر المادة مع Batch نفسه
            entity.HasIndex(x => new
            {
                x.InspectionBookId,
                x.MaterialId,
                x.NormalizedBatchNumber
            })
            .IsUnique();
        });
    }
}
