using System.Security.Claims;
using Identity.Application.Abstractions;
using Identity.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Quorum.Core.Auth;

namespace Identity.Infrastructure.Security;

internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : ITokenIssuer
{
    public AccessToken Issue(User user)
    {
        var jwt = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(jwt.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email.Value),
            new("name", user.FullName),
        };
        claims.AddRange(user.Roles.Select(r => new Claim("role", r)));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(jwt.SigningKey, SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, expires);
    }
}
