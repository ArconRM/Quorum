using Identity.Domain.Users;

namespace Identity.Application.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public interface ITokenIssuer
{
    AccessToken Issue(User user);
}
