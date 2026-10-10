using Identity.Application.Abstractions;
using Identity.Application.Models;
using MediatR;
using Quorum.Application.Abstractions;
using Quorum.Application.Exceptions;

namespace Identity.Application.Features.Users.RevokeRole;

public sealed class RevokeRoleHandler(IUserRepository users, IUnitOfWork uow) : IRequestHandler<RevokeRoleCommand, UserDto>
{
    public async Task<UserDto> Handle(RevokeRoleCommand cmd, CancellationToken ct)
    {
        var user = await users.GetAsync(cmd.UserId, ct) ?? throw new NotFoundException($"User {cmd.UserId} not found");
        user.RevokeRole(cmd.Role);
        await uow.CommitAsync(ct);
        return UserDto.From(user);
    }
}
