using Identity.Domain.Users;

namespace Identity.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken ct);
    Task<User?> FindByEmailAsync(Email email, CancellationToken ct);
    Task<bool> ExistsAsync(Email email, CancellationToken ct);
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);
    void Add(User user);
}
