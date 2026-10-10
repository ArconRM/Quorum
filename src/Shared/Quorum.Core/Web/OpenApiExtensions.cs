using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Quorum.Core.Web;

public static class OpenApiExtensions
{
    /// <summary>OpenAPI-документ (встроенный генератор ASP.NET Core) + схема Bearer JWT.</summary>
    public static IServiceCollection AddQuorumOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(o => o.AddDocumentTransformer<BearerAndServerTransformer>());
        return services;
    }

    /// <summary>/openapi/v1.json + Swagger UI на /swagger.</summary>
    public static WebApplication UseQuorumSwagger(this WebApplication app)
    {
        app.MapOpenApi();
        app.UseSwaggerUI(o =>
        {
            o.SwaggerEndpoint("/openapi/v1.json", app.Environment.ApplicationName);
            o.RoutePrefix = "swagger";
        });
        return app;
    }
}

/// <summary>
/// Добавляет кнопку Authorize (Bearer) и, если задан OpenApi:ServerUrl (например, "/catalog"),
/// подставляет адрес сервера — чтобы «Try it out» из общего Swagger UI на gateway ходил через префикс.
/// </summary>
internal sealed class BearerAndServerTransformer(IConfiguration config) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Токен из POST /identity/auth/login",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });

        var serverUrl = config["OpenApi:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(serverUrl))
            document.Servers = [new OpenApiServer { Url = serverUrl }];

        return Task.CompletedTask;
    }
}
