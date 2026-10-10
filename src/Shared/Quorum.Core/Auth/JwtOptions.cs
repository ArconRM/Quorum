using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Quorum.Core.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "quorum-identity";
    public string Audience { get; set; } = "quorum";
    public string Key { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 120;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}
