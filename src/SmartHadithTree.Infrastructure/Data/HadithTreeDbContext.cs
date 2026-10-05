using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data;

/// <summary>
/// EF Core database context for the Smart Hadith Tree application.
/// Configured with Arabic_100_CI_AI collation for accent-insensitive Arabic text searches.
/// </summary>
public class HadithTreeDbContext : DbContext, IHadithTreeDbContext
{
    public HadithTreeDbContext(DbContextOptions<HadithTreeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Narrator> Narrators => Set<Narrator>();
    public DbSet<HadithText> Hadiths => Set<HadithText>();
    public DbSet<Transmission> Transmissions => Set<Transmission>();
    public DbSet<ScholarEvaluation> ScholarEvaluations => Set<ScholarEvaluation>();
    public DbSet<HadithCluster> HadithClusters => Set<HadithCluster>();
    public DbSet<NarratorRelation> NarratorRelations => Set<NarratorRelation>();
    public DbSet<MukhtalitHearing> MukhtalitHearings => Set<MukhtalitHearing>();
    public DbSet<MukhtalitGroupRule> MukhtalitGroupRules => Set<MukhtalitGroupRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Database-wide Arabic collation: makes all text comparisons
        // accent-insensitive, so searching "محمد" matches "مُحَمَّدٌ" (with Tashkeel).
        modelBuilder.UseCollation("Arabic_100_CI_AI");

        // Apply all IEntityTypeConfiguration<T> implementations from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HadithTreeDbContext).Assembly);
    }
}
