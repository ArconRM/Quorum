using Datasets.Domain.Frames;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datasets.Infrastructure.Db.Configurations;

internal sealed class FrameConfiguration : IEntityTypeConfiguration<Frame>
{
    public void Configure(EntityTypeBuilder<Frame> e)
    {
        e.ToTable("frames");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.StoragePath).HasMaxLength(500);
        e.Property(x => x.ContentType).HasMaxLength(64);
        // «новые кадры камеры» = WHERE camera_id = @id AND snapshot_id IS NULL ORDER BY uploaded_at
        e.HasIndex(x => new { x.CameraId, x.SnapshotId, x.UploadedAt });
        e.HasIndex(x => x.SnapshotId);
        e.Property(x => x.Version).IsRowVersion(); // Npgsql: uint + IsRowVersion → xmin
    }
}
