using Quorum.Core.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Quorum.Core.Web;

public static class ServiceDefaults
{
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
        builder.Services.AddJwtAuth(builder.Configuration);
        builder.Services.AddHealthChecks();
        builder.Services.AddQuorumOpenApi();
        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health");
        app.UseQuorumSwagger();
        return app;
    }
}
