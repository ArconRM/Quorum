using Identity.Application.Abstractions;
using Identity.Application.Features.Auth.Register;
using Identity.Domain.Users;
using NSubstitute;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Domain;

namespace Identity.UnitTests.Features;

public class RegisterUserHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IIntegrationEventPublisher _events = Substitute.For<IIntegrationEventPublisher>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private RegisterUserHandler Handler() => new(_users, _hasher, _events, _uow);

    [Fact]
    public async Task Taken_email_fails_and_publishes_nothing()
    {
        _users.ExistsAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<DomainException>(() =>
            Handler().Handle(new RegisterUserCommand("x@y.ru", "password1", "X"), CancellationToken.None));

        await _events.DidNotReceiveWithAnyArgs().PublishAsync<UserRegistered>(default!, default);
        await _uow.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task Publishes_UserRegistered_with_annotator_role_and_commits()
    {
        _hasher.Hash(Arg.Any<User>(), Arg.Any<string>()).Returns("hash");

        var id = await Handler().Handle(new RegisterUserCommand("new@user.ru", "password1", "New"), CancellationToken.None);

        _users.Received(1).Add(Arg.Is<User>(u => u.Id == id && u.PasswordHash == "hash"));
        await _events.Received(1).PublishAsync(
            Arg.Is<UserRegistered>(e => e.UserId == id && e.Roles.Contains(Role.Annotator)), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
