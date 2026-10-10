using Identity.Application.Abstractions;
using Identity.Application.Models;
using MediatR;

namespace Identity.Application.Features.Users.ListUsers;

public sealed class ListUsersHandler(IUserRepository users) : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery q, CancellationToken ct) =>
        (await users.ListAsync(ct)).Select(UserDto.From).ToList();
}
