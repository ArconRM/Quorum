using Labeling.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Db.Configurations;

internal sealed class LabelingRunConfiguration : IEntityTypeConfiguration<LabelingRun>
{
    public void Configure(EntityTypeBuilder<LabelingRun> e)
    {
        e.ToTable("runs");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.CycleId).IsUnique();
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Prompt).HasMaxLength(1000);
        e.Property(x => x.FailureReason).HasMaxLength(2000);
        e.Property(x => x.Version).IsRowVersion();
        e.Ignore(x => x.TotalWeight);
        e.Ignore(x => x.AllTeachersCompleted);

        e.OwnsOne(x => x.Strategy, s =>
        {
            s.Property(x => x.Mode).HasColumnName("validation_mode").HasConversion<string>().HasMaxLength(16);
            s.Property(x => x.AgreementThreshold).HasColumnName("agreement_threshold");
        });
        e.Navigation(x => x.Strategy).IsRequired();

        e.OwnsMany(x => x.Teachers, t =>
        {
            t.ToTable("run_teachers");
            t.WithOwner().HasForeignKey("RunId");
            t.HasKey("RunId", nameof(RunTeacher.TeacherId));
            t.Property(x => x.Name).HasMaxLength(200);
            t.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        });
        e.Navigation(x => x.Teachers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
