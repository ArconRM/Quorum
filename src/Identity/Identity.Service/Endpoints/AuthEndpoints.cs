using System.Security.Claims;
using Identity.Application.Features.Auth.Login;
using Identity.Application.Features.Auth.Register;
using MediatR;
using Quorum.Core.Auth;

namespace Identity.Service.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterUserCommand cmd, ISender sender, CancellationToken ct) =>
            Results.Ok(new { id = await sender.Send(cmd, ct) }));

        group.MapPost("/login", async (LoginCommand cmd, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(cmd, ct)));

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            id = user.GetUserId(),
            email = user.FindFirstValue("email"),
            name = user.FindFirstValue("name"),
            roles = user.FindAll("role").Select(c => c.Value),
        })).RequireAuthorization();

        return app;
    }
}
