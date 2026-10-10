using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Cycles;

namespace Training.Infrastructure.Db.Configurations;

internal sealed class AdaptationCycleConfiguration : IEntityTypeConfiguration<AdaptationCycle>
{
    public void Configure(EntityTypeBuilder<AdaptationCycle> e)
    {
        e.ToTable("cycles");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Trigger).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.FailureReason).HasMaxLength(2000);
        e.Property(x => x.Version).IsRowVersion();
        e.Ignore(x => x.IsFinished);

        e.OwnsOne(x => x.Validation, v =>
        {
            v.Property(x => x.Mode).HasColumnName("validation_mode").HasConversion<string>().HasMaxLength(16);
            v.Property(x => x.AgreementThreshold).HasColumnName("agreement_threshold");
        });
        e.Navigation(x => x.Validation).IsRequired();

        e.HasIndex(x => new { x.CameraId, x.StartedAt });
        // Инвариант «один активный цикл на камеру» — ещё и на уровне БД (защита от гонки двух стартов).
        e.HasIndex(x => x.CameraId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Snapshotting', 'Labeling', 'Training', 'Evaluating')")
            .HasDatabaseName("ix_cycles_one_active_per_camera");
    }
}
