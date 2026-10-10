using Identity.Application.Abstractions;
using Identity.Domain.Users;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Contracts;
using Quorum.Domain;

namespace Identity.Application.Features.Auth.Register;

public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IIntegrationEventPublisher events,
    IUnitOfWork uow) : IRequestHandler<RegisterUserCommand, Guid>
{
    public async Task<Guid> Handle(RegisterUserCommand cmd, CancellationToken ct)
    {
        var email = Email.Create(cmd.Email);
        if (await users.ExistsAsync(email, ct)) throw new DomainException("User with this email already exists");

        var user = User.Register(email, cmd.FullName);
        user.SetPasswordHash(hasher.Hash(user, cmd.Password));
        users.Add(user);

        // Событие ляжет в outbox в той же транзакции, что и пользователь.
        await events.PublishAsync(new UserRegistered(user.Id, user.Email.Value, user.FullName, user.Roles.ToList()), ct);
        await uow.CommitAsync(ct);
        return user.Id;
    }
}
