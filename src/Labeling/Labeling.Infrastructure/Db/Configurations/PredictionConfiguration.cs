using Labeling.Domain.Predictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Db.Configurations;

internal sealed class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> e)
    {
        e.ToTable("predictions");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        // один ответ учителя на кадр в прогоне — защита от дублей при повторной доставке
        e.HasIndex(x => new { x.RunId, x.TeacherId, x.FrameId }).IsUnique();
        e.HasIndex(x => new { x.RunId, x.FrameId });
        e.Property(x => x.Text).HasMaxLength(64);
        e.OwnsMany(x => x.Boxes, box =>
        {
            box.ToJson("boxes"); // jsonb: [{ "X":…, "Y":…, "W":…, "H":…, "Score":… }]
            box.Ignore(x => x.Area);
        });
        e.Navigation(x => x.Boxes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
