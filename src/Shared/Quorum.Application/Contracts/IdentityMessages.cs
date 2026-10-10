namespace Quorum.Application.Contracts;

public sealed record UserRegistered(Guid UserId, string Email, string FullName, IReadOnlyList<string> Roles);
