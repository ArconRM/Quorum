using Datasets.Domain.Cameras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datasets.Infrastructure.Db.Configurations;

internal sealed class CameraConfiguration : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> e)
    {
        e.ToTable("cameras");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Name).HasMaxLength(200);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.Prompt).HasMaxLength(1000);
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(32);
    }
}
