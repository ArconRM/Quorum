using Quorum.Domain;

namespace Identity.Domain.Users;

/// <summary>Корень агрегата: пользователь системы.</summary>
public sealed class User : Entity
{
    private List<string> _roles = [];

    private User() { }

    private User(Guid id, Email email, string fullName) : base(id)
    {
        Email = email;
        FullName = fullName;
        CreatedAt = DateTime.UtcNow;
    }

    public Email Email { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<string> Roles => _roles.AsReadOnly();

    /// <summary>Саморегистрация: всегда разметчик. Остальные роли выдаёт администратор.</summary>
    public static User Register(Email email, string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Full name is required");
        var user = new User(Guid.NewGuid(), email, fullName.Trim());
        user._roles.Add(Role.Annotator);
        return user;
    }

    public static User CreateAdmin(Email email, string fullName)
    {
        var user = new User(Guid.NewGuid(), email, fullName);
        user._roles.Add(Role.Admin);
        return user;
    }

    public void SetPasswordHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) throw new DomainException("Password hash is required");
        PasswordHash = hash;
    }

    public void AssignRole(string role)
    {
        if (!Role.All.Contains(role)) throw new DomainException($"Unknown role '{role}'");
        if (!_roles.Contains(role)) _roles.Add(role);
    }

    public void RevokeRole(string role)
    {
        if (!_roles.Contains(role)) return;
        if (_roles.Count == 1) throw new DomainException("User must have at least one role");
        _roles.Remove(role);
    }
}
