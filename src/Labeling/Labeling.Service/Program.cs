using Labeling.Application.Features.Runs.StartLabelingRun;
using Labeling.Infrastructure;
using Labeling.Infrastructure.Db;
using Labeling.Service.Endpoints;
using Labeling.Service.Grpc;
using Quorum.Application;
using Quorum.Core.Persistence;
using Quorum.Core.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddGrpc();
builder.Services.AddApplicationLayer(typeof(StartLabelingRunCommand).Assembly);
builder.Services.AddLabelingInfrastructure();

var app = builder.Build();
app.UseServiceDefaults();
await app.Services.MigrateDatabaseAsync<LabelingDbContext>();

app.MapGrpcService<TrainingSetQueryGrpcService>();
app.MapTeacherEndpoints();
app.MapRunEndpoints();
app.MapReviewEndpoints();

app.Run();

public partial class Program;
