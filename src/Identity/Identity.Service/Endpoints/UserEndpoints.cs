using Identity.Application.Features.Users.AssignRole;
using Identity.Application.Features.Users.ListUsers;
using Identity.Application.Features.Users.RevokeRole;
using MediatR;
using Quorum.Core.Auth;

namespace Identity.Service.Endpoints;

public sealed record RoleRequest(string Role);

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users").RequireAuthorization(p => p.RequireRole(Roles.Admin));

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListUsersQuery(), ct)));

        group.MapPost("/{id:guid}/roles", async (Guid id, RoleRequest req, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new AssignRoleCommand(id, req.Role), ct)));

        group.MapDelete("/{id:guid}/roles/{role}", async (Guid id, string role, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new RevokeRoleCommand(id, role), ct)));

        return app;
    }
}
