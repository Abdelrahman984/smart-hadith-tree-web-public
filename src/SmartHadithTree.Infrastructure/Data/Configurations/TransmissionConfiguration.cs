using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Transmission"/> entity.
/// This is the critical ternary relationship: Sheikh → Student for a Hadith at a step.
/// </summary>
/// <remarks>
/// Both SheikhId and StudentId point to the Narrator table. SQL Server rejects
/// CASCADE delete on multiple FK paths to the same table, so both narrator FKs
/// use <see cref="DeleteBehavior.Restrict"/>.
/// </remarks>
public class TransmissionConfiguration : IEntityTypeConfiguration<Transmission>
{
    public void Configure(EntityTypeBuilder<Transmission> builder)
    {
        builder.ToTable("Transmissions");

        // ── Primary Key ────────────────────────────────────────────
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            ;

        // ── Relationships ──────────────────────────────────────────

        // 1. Hadith → Transmissions (one-to-many, cascade delete allowed).
        //    Deleting a Hadith removes all its chain steps.
        builder.HasOne(t => t.Hadith)
            .WithMany(h => h.Transmissions)
            .HasForeignKey(t => t.HadithId)
            .OnDelete(DeleteBehavior.Cascade);

        // 2. Sheikh (Narrator as teacher) → Transmissions.
        //    RESTRICT: prevents SQL Server "multiple cascade paths" error.
        builder.HasOne(t => t.Sheikh)
            .WithMany(n => n.TransmissionsAsSheikh)
            .HasForeignKey(t => t.SheikhId)
            .OnDelete(DeleteBehavior.Restrict);

        // 3. Student (Narrator as receiver) → Transmissions.
        //    RESTRICT: same reason as above.
        builder.HasOne(t => t.Student)
            .WithMany(n => n.TransmissionsAsStudent)
            .HasForeignKey(t => t.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Properties ─────────────────────────────────────────────
        builder.Property(t => t.TransmissionTerm)
            .HasMaxLength(50)
            .UseCollation("Arabic_100_CI_AI");

        // ── Indexes ────────────────────────────────────────────────

        // Allow multiple branches in a Hadith's chain (not unique).
        builder.HasIndex(t => new { t.HadithId, t.StepOrder })
            .HasDatabaseName("IX_Transmissions_HadithId_StepOrder");

        // Bidirectional narrator traversal queries:
        // "Find all students of sheikh X" and "Find all sheikhs of student Y".
        builder.HasIndex(t => new { t.SheikhId, t.StudentId })
            .HasDatabaseName("IX_Transmissions_SheikhId_StudentId");

        builder.HasIndex(t => new { t.StudentId, t.SheikhId })
            .HasDatabaseName("IX_Transmissions_StudentId_SheikhId");
    }
}
