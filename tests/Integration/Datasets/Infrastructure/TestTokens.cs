using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Datasets.IntegrationTests.Infrastructure;

/// <summary>Выпускает JWT тем же dev-ключом, что прописан в appsettings.json сервиса (вместо похода в Identity).</summary>
public static class TestTokens
{
    public const string Key = "dev-only-super-secret-signing-key-change-me-0123456789";

    public static string For(params string[] roles)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "quorum-identity",
            Audience = "quorum",
            Expires = DateTime.UtcNow.AddMinutes(10),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256),
        });
    }
}
