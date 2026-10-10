using Identity.Application.Models;
using MediatR;

namespace Identity.Application.Features.Users.ListUsers;

public sealed record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>;
