using Labeling.Domain.Labels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Db.Configurations;

internal sealed class ConsensusLabelConfiguration : IEntityTypeConfiguration<ConsensusLabel>
{
    public void Configure(EntityTypeBuilder<ConsensusLabel> e)
    {
        e.ToTable("labels");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => new { x.RunId, x.FrameId }).IsUnique();
        e.HasIndex(x => new { x.RunId, x.Status });
        e.Property(x => x.FramePath).HasMaxLength(500);
        e.Property(x => x.Text).HasMaxLength(64);
        e.Property(x => x.Split).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
        e.OwnsMany(x => x.Boxes, box =>
        {
            box.ToJson("boxes");
            box.Ignore(x => x.Area);
        });
        e.Navigation(x => x.Boxes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
