using Identity.Application.Abstractions;
using Identity.Application.Models;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Exceptions;

namespace Identity.Application.Features.Users.AssignRole;

/// <summary>Новая роль попадёт в токен при следующем входе пользователя.</summary>
public sealed class AssignRoleHandler(IUserRepository users, IUnitOfWork uow) : IRequestHandler<AssignRoleCommand, UserDto>
{
    public async Task<UserDto> Handle(AssignRoleCommand cmd, CancellationToken ct)
    {
        var user = await users.GetAsync(cmd.UserId, ct) ?? throw new NotFoundException($"User {cmd.UserId} not found");
        user.AssignRole(cmd.Role);
        await uow.CommitAsync(ct);
        return UserDto.From(user);
    }
}
