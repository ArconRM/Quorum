using Datasets.Domain.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datasets.Infrastructure.Db.Configurations;

internal sealed class DatasetSnapshotConfiguration : IEntityTypeConfiguration<DatasetSnapshot>
{
    public void Configure(EntityTypeBuilder<DatasetSnapshot> e)
    {
        e.ToTable("snapshots");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.CycleId).IsUnique();
        e.HasIndex(x => new { x.CameraId, x.Status });
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Prompt).HasMaxLength(1000);
        e.Ignore(x => x.TrainCount);
        e.Ignore(x => x.HoldoutCount);
        e.OwnsMany(x => x.Frames, f =>
        {
            f.ToTable("snapshot_frames");
            f.WithOwner().HasForeignKey("SnapshotId");
            f.HasKey("SnapshotId", nameof(SnapshotFrame.FrameId));
            f.Property(x => x.StoragePath).HasMaxLength(500);
            f.Property(x => x.Split).HasConversion<string>().HasMaxLength(16);
        });
        e.Navigation(x => x.Frames).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
