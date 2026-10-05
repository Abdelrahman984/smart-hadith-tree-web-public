using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="HadithText"/> entity.
/// </summary>
public class HadithTextConfiguration : IEntityTypeConfiguration<HadithText>
{
    public void Configure(EntityTypeBuilder<HadithText> builder)
    {
        builder.ToTable("Hadiths");

        // ── Primary Key ────────────────────────────────────────────
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            ;

        // ── String Properties ──────────────────────────────────────
        builder.Property(h => h.MatnArabic)
            .IsRequired()
            .UseCollation("Arabic_100_CI_AI");
        builder.Property(h => h.NormalizedMatn)
            .IsRequired()
            .UseCollation("Arabic_100_BIN2");
        // MatnArabic is nvarchar(max) by default — Hadith texts can be lengthy.

        builder.Property(h => h.BookName)
            .IsRequired()
            .HasMaxLength(300)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(h => h.NormalizedBookName)
            .IsRequired()
            .HasMaxLength(300)
            .UseCollation("Arabic_100_BIN2");

        builder.Property(h => h.Volume)
            .HasMaxLength(50);

        builder.Property(h => h.Chapter)
            .HasMaxLength(500)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(h => h.FullIsnadText)
            .UseCollation("Arabic_100_BIN2");

        // ── Indexes ────────────────────────────────────────────────
        // Composite index for lookup-by-reference (book + number).
        builder.HasIndex(h => new { h.BookName, h.HadithNumber })
            .HasDatabaseName("IX_Hadiths_BookName_HadithNumber");

        // HadithNumber alone for direct-number searches.
        builder.HasIndex(h => h.HadithNumber)
            .HasDatabaseName("IX_Hadiths_HadithNumber");

        // NormalizedBookName index for fast book filtering.
        builder.HasIndex(h => h.NormalizedBookName)
            .HasDatabaseName("IX_Hadiths_NormalizedBookName");
    }
}
