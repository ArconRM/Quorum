using System.Text.RegularExpressions;
using Quorum.Domain;

namespace Identity.Domain.Users;

/// <summary>Value object: e-mail в нормализованном виде.</summary>
public sealed partial record Email
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new DomainException("Email is required");
        var value = raw.Trim().ToLowerInvariant();
        if (!EmailRegex().IsMatch(value)) throw new DomainException($"'{raw}' is not a valid email");
        return new Email(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
