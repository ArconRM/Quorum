using Labeling.Domain.Teachers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Labeling.Infrastructure.Db.Configurations;

internal sealed class TeacherModelConfiguration : IEntityTypeConfiguration<TeacherModel>
{
    public void Configure(EntityTypeBuilder<TeacherModel> e)
    {
        e.ToTable("teachers");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Name).HasMaxLength(200);
        e.HasIndex(x => x.Name).IsUnique();
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
        e.Property(x => x.Task).HasConversion<string>().HasMaxLength(32);
    }
}
