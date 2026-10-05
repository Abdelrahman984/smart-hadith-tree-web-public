using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Narrator"/> entity.
/// </summary>
public class NarratorConfiguration : IEntityTypeConfiguration<Narrator>
{
    public void Configure(EntityTypeBuilder<Narrator> builder)
    {
        builder.ToTable("Narrators");

        // ── Primary Key ────────────────────────────────────────────
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            ;

        // ── String Properties with Arabic Collation ────────────────
        builder.Property(n => n.FullName)
            .IsRequired()
            .HasMaxLength(500)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(n => n.KnownAs)
            .HasMaxLength(200)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(n => n.Kunyah)
            .HasMaxLength(200)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(n => n.GenerationTier)
            .HasMaxLength(150)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(n => n.BirthPlace)
            .HasMaxLength(200)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(n => n.DeathPlace)
            .HasMaxLength(200)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(n => n.Biography)
            .UseCollation("Arabic_100_CI_AI");

        // ── Indexes ────────────────────────────────────────────────
        // Primary search index on full name.
        builder.HasIndex(n => n.FullName)
            .HasDatabaseName("IX_Narrators_FullName");

        // Filtering by death year (common in Hadith scholarship).
        builder.HasIndex(n => n.DeathYearHijri)
            .HasDatabaseName("IX_Narrators_DeathYearHijri");

        // KnownAs for alias-based searches.
        builder.HasIndex(n => n.KnownAs)
            .HasDatabaseName("IX_Narrators_KnownAs");

        builder.Property(n => n.ItqanGrade)
            .HasMaxLength(50);

        builder.Property(n => n.IkhtilatNote)
            .HasMaxLength(1000)
            .UseCollation("Arabic_100_CI_AI");

        // ── Shamela registry (Phase 5) ─────────────────────────────
        builder.Property(n => n.SourceKey)
            .HasMaxLength(40);

        builder.Property(n => n.Verdict)
            .HasMaxLength(500)
            .UseCollation("Arabic_100_CI_AI");

        // Filtered so that rows without a registry id (legacy Itqan rows) do not collide.
        builder.HasIndex(n => n.SourceKey)
            .IsUnique()
            .HasFilter("[SourceKey] IS NOT NULL")
            .HasDatabaseName("IX_Narrators_SourceKey");

        builder.HasIndex(n => n.ShamelaManId)
            .HasDatabaseName("IX_Narrators_ShamelaManId");

        builder.HasIndex(n => n.ItqanId)
            .IsUnique()
            .HasDatabaseName("IX_Narrators_ItqanId");
    }
}
