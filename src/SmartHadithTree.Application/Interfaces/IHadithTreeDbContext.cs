using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Application.Interfaces;

public interface IHadithTreeDbContext
{
    DbSet<HadithText> Hadiths { get; }
    DbSet<Narrator> Narrators { get; }
    DbSet<Transmission> Transmissions { get; }
    DbSet<ScholarEvaluation> ScholarEvaluations { get; }
    DbSet<HadithCluster> HadithClusters { get; }
    DbSet<NarratorRelation> NarratorRelations { get; }
    DbSet<MukhtalitHearing> MukhtalitHearings { get; }
    DbSet<MukhtalitGroupRule> MukhtalitGroupRules { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
