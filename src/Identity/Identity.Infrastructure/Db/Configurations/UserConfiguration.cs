using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Db.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Email).HasConversion(v => v.Value, v => Email.Create(v)).HasMaxLength(256);
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.FullName).HasMaxLength(200);
        e.Ignore(x => x.Roles);
        e.Property<List<string>>("_roles").HasColumnName("roles"); // text[] в Postgres
    }
}
