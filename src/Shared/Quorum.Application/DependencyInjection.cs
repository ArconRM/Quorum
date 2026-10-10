using System.Reflection;
using FluentValidation;
using Quorum.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace Quorum.Application;

public static class DependencyInjection
{
    /// <summary>MediatR-хендлеры и валидаторы из сборки Application конкретного сервиса.</summary>
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services, Assembly assembly)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
