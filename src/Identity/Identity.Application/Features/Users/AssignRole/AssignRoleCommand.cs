using Identity.Application.Models;
using MediatR;

namespace Identity.Application.Features.Users.AssignRole;

public sealed record AssignRoleCommand(Guid UserId, string Role) : IRequest<UserDto>;
