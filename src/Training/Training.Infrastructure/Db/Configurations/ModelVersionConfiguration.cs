using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Training.Domain.Models;

namespace Training.Infrastructure.Db.Configurations;

internal sealed class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    public void Configure(EntityTypeBuilder<ModelVersion> e)
    {
        e.ToTable("model_versions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => new { x.CameraId, x.Number }).IsUnique();
        // Инвариант «одна опубликованная версия на камеру» — и в домене, и в БД.
        e.HasIndex(x => x.CameraId).IsUnique().HasFilter("\"Status\" = 'Published'").HasDatabaseName("ix_versions_one_published_per_camera");
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Architecture).HasMaxLength(64);
        e.Property(x => x.ArtifactPath).HasMaxLength(500);
        e.Property(x => x.MetricName).HasMaxLength(64);
        e.Property(x => x.Version).IsRowVersion();
    }
}
