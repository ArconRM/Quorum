using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Identity.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

internal sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetAsync(Guid id, CancellationToken ct) => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByEmailAsync(Email email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> ExistsAsync(Email email, CancellationToken ct) => db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public void Add(User user) => db.Users.Add(user);
}
