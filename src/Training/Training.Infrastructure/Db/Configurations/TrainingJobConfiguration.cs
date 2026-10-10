using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Jobs;

namespace Training.Infrastructure.Db.Configurations;

internal sealed class TrainingJobConfiguration : IEntityTypeConfiguration<TrainingJob>
{
    public void Configure(EntityTypeBuilder<TrainingJob> e)
    {
        e.ToTable("training_jobs");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.CycleId).IsUnique();
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Architecture).HasMaxLength(64);
        e.Property(x => x.BaseArtifact).HasMaxLength(500);
        e.Property(x => x.ArtifactPath).HasMaxLength(500);
        e.Property(x => x.MetricName).HasMaxLength(64);
        e.Property(x => x.FailureReason).HasMaxLength(2000);
    }
}
