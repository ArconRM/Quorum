using Identity.Application.Features.Auth.Register;
using Identity.Infrastructure;
using Identity.Infrastructure.Db;
using Identity.Infrastructure.Seeding;
using Identity.Service.Endpoints;
using Quorum.Application;
using Quorum.Core.Persistence;
using Quorum.Core.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApplicationLayer(typeof(RegisterUserCommand).Assembly);
builder.Services.AddIdentityInfrastructure();

var app = builder.Build();
app.UseServiceDefaults();
await app.Services.MigrateDatabaseAsync<IdentityDbContext>();
await app.Services.SeedAdminAsync();

app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program;
