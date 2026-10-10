using Identity.Domain.Users;
using Quorum.Domain;

namespace Identity.UnitTests.Domain;

public class UserTests
{
    [Theory]
    [InlineData("  Ann@Example.COM ", "ann@example.com")]
    [InlineData("a@b.ru", "a@b.ru")]
    public void Email_is_normalized(string raw, string expected) => Assert.Equal(expected, Email.Create(raw).Value);

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    public void Invalid_email_is_rejected(string raw) => Assert.Throws<DomainException>(() => Email.Create(raw));

    [Fact]
    public void Self_registered_user_is_annotator()
    {
        var user = User.Register(Email.Create("ann@quorum.local"), "Ann");
        Assert.Equal(new[] { Role.Annotator }, user.Roles);
    }

    [Fact]
    public void Admin_can_grant_operator_and_role_is_not_duplicated()
    {
        var user = User.Register(Email.Create("op@quorum.local"), "Op");
        user.AssignRole(Role.Operator);
        user.AssignRole(Role.Operator);
        Assert.Equal(2, user.Roles.Count);
    }

    [Fact]
    public void Unknown_role_is_rejected() =>
        Assert.Throws<DomainException>(() => User.Register(Email.Create("x@y.ru"), "X").AssignRole("Superuser"));

    [Fact]
    public void Last_role_cannot_be_revoked()
    {
        var user = User.Register(Email.Create("x@y.ru"), "X");
        Assert.Throws<DomainException>(() => user.RevokeRole(Role.Annotator));
    }
}
