using Quorum.Application;
using Quorum.Core.Persistence;
using Quorum.Core.Web;
using Training.Application.Features.Cycles.StartCycle;
using Training.Infrastructure;
using Training.Infrastructure.Db;
using Training.Service.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApplicationLayer(typeof(StartCycleCommand).Assembly);
builder.Services.AddTrainingInfrastructure();

var app = builder.Build();
app.UseServiceDefaults();
await app.Services.MigrateDatabaseAsync<TrainingDbContext>();

app.MapCycleEndpoints();
app.MapModelEndpoints();

app.Run();

public partial class Program;
