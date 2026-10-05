using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="ScholarEvaluation"/> entity.
/// </summary>
public class ScholarEvaluationConfiguration : IEntityTypeConfiguration<ScholarEvaluation>
{
    public void Configure(EntityTypeBuilder<ScholarEvaluation> builder)
    {
        builder.ToTable("ScholarEvaluations");

        // ── Primary Key ────────────────────────────────────────────
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            ;

        // ── Relationship ───────────────────────────────────────────
        // Cascade delete: removing a Narrator removes all their evaluations.
        builder.HasOne(e => e.Narrator)
            .WithMany(n => n.ScholarEvaluations)
            .HasForeignKey(e => e.NarratorId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── String Properties ──────────────────────────────────────
        builder.Property(e => e.ScholarName)
            .IsRequired()
            .HasMaxLength(300)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(e => e.EvaluationText)
            .IsRequired()
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(e => e.SourceBook)
            .HasMaxLength(300)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(e => e.SourceVolume)
            .HasMaxLength(20);

        builder.Property(e => e.VerdictRating)
            .HasMaxLength(100)
            .UseCollation("Arabic_100_CI_AI");

        // ── Indexes ────────────────────────────────────────────────
        // Fast retrieval of all evaluations for a narrator (used by RAG pipeline).
        builder.HasIndex(e => e.NarratorId)
            .HasDatabaseName("IX_ScholarEvaluations_NarratorId");

        // Filter/group by scholar name.
        builder.HasIndex(e => e.ScholarName)
            .HasDatabaseName("IX_ScholarEvaluations_ScholarName");
    }
}
