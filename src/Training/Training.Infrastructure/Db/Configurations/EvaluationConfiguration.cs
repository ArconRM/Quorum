using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Evaluations;

namespace Training.Infrastructure.Db.Configurations;

internal sealed class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> e)
    {
        e.ToTable("evaluations");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.CycleId).IsUnique();
        e.Property(x => x.MetricName).HasMaxLength(64);
        e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(16);
    }
}
