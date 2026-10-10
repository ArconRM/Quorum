using Identity.Application.Abstractions;
using Identity.Domain.Users;
using MediatR;

namespace Identity.Application.Features.Auth.Login;

public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher, ITokenIssuer tokens)
    : IRequestHandler<LoginCommand, AccessToken>
{
    public async Task<AccessToken> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(Email.Create(cmd.Email), ct);
        if (user is null || !hasher.Verify(user, cmd.Password))
            throw new UnauthorizedAccessException("Invalid email or password");
        return tokens.Issue(user);
    }
}
