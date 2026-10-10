using Identity.Domain.Users;

namespace Identity.Application.Models;

public sealed record UserDto(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles, DateTime CreatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Email.Value, u.FullName, u.Roles.ToList(), u.CreatedAt);
}
