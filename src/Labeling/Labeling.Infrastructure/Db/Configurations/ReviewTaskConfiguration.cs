using Labeling.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Db.Configurations;

internal sealed class ReviewTaskConfiguration : IEntityTypeConfiguration<ReviewTask>
{
    public void Configure(EntityTypeBuilder<ReviewTask> e)
    {
        e.ToTable("review_tasks");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.HasIndex(x => x.LabelId).IsUnique();
        e.HasIndex(x => new { x.Status, x.CreatedAt });
        e.HasIndex(x => new { x.AssigneeId, x.Status });
        e.HasIndex(x => new { x.RunId, x.Status });
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Verdict).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.Version).IsRowVersion();
    }
}
