using Identity.Application.Abstractions;
using Identity.Infrastructure.Db;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Quorum.Core.Messaging;
using Quorum.Core.Persistence;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddPostgres<IdentityDbContext>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddMessaging<IdentityDbContext>();
        return services;
    }
}
