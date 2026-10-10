using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Identity.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Seeding;

public static class AdminSeeder
{
    /// <summary>Сид администратора из конфигурации (Admin:Email / Admin:Password).</summary>
    public static async Task SeedAdminAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var emailRaw = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(emailRaw) || string.IsNullOrWhiteSpace(password)) return;

        var email = Email.Create(emailRaw);
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        var admin = User.CreateAdmin(email, config["Admin:FullName"] ?? "Administrator");
        admin.SetPasswordHash(hasher.Hash(admin, password));
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
