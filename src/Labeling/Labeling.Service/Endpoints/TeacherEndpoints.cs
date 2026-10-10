using Labeling.Application.Features.Teachers.ListTeachers;
using Labeling.Application.Features.Teachers.RegisterTeacher;
using Labeling.Application.Features.Teachers.UpdateTeacher;
using MediatR;
using Quorum.Core.Auth;

namespace Labeling.Service.Endpoints;

public sealed record UpdateTeacherRequest(double? Weight, bool? IsActive);

public static class TeacherEndpoints
{
    public static IEndpointRouteBuilder MapTeacherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/teachers").WithTags("Teachers").RequireAuthorization();

        group.MapGet("/", async (string? task, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListTeachersQuery(task), ct)));

        group.MapPost("/", async (RegisterTeacherCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(cmd, ct);
            return Results.Created($"/teachers/{id}", new { id });
        }).RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        group.MapPatch("/{id:guid}", async (Guid id, UpdateTeacherRequest req, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateTeacherCommand(id, req.Weight, req.IsActive), ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Operator, Roles.Admin));

        return app;
    }
}
