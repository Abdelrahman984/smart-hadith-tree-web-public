using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="MukhtalitGroupRule"/> entity.
/// </summary>
public class MukhtalitGroupRuleConfiguration : IEntityTypeConfiguration<MukhtalitGroupRule>
{
    public void Configure(EntityTypeBuilder<MukhtalitGroupRule> builder)
    {
        builder.ToTable("MukhtalitGroupRules");

        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.Mukhtalit)
            .WithMany(n => n.MukhtalitGroupRules)
            .HasForeignKey(r => r.MukhtalitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.GroupAr)
            .IsRequired()
            .HasMaxLength(300)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(r => r.Quote)
            .IsRequired()
            .HasMaxLength(1000)
            .UseCollation("Arabic_100_CI_AI");

        builder.Property(r => r.SourceBook)
            .HasMaxLength(300)
            .UseCollation("Arabic_100_CI_AI");

        builder.HasIndex(r => r.MukhtalitId)
            .HasDatabaseName("IX_MukhtalitGroupRules_MukhtalitId");
    }
}
