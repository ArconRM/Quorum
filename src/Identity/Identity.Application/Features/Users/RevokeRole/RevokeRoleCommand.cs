using Identity.Application.Models;
using MediatR;

namespace Identity.Application.Features.Users.RevokeRole;

public sealed record RevokeRoleCommand(Guid UserId, string Role) : IRequest<UserDto>;
