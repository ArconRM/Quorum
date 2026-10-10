using Datasets.Application.Features.Cameras.CreateCamera;
using Datasets.Infrastructure;
using Datasets.Infrastructure.Db;
using Datasets.Service.Endpoints;
using Datasets.Service.Grpc;
using Quorum.Application;
using Quorum.Core.Persistence;
using Quorum.Core.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddGrpc();
builder.Services.AddApplicationLayer(typeof(CreateCameraCommand).Assembly);
builder.Services.AddDatasetsInfrastructure();

var app = builder.Build();
app.UseServiceDefaults();
await app.Services.MigrateDatabaseAsync<DatasetsDbContext>();

app.MapGrpcService<SnapshotQueryGrpcService>();
app.MapCameraEndpoints();
app.MapFrameEndpoints();
app.MapSnapshotEndpoints();

app.Run();

public partial class Program;
