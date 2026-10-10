// API Gateway на YARP: единая точка входа + общий Swagger UI по всем сервисам.
// JWT здесь не проверяется — каждый сервис валидирует токен сам.
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

var services = builder.Configuration.GetSection("ReverseProxy:Routes").GetChildren().Select(r => r.Key).ToArray();
app.UseSwaggerUI(o =>
{
    // документы тянутся через тот же прокси: /labeling/openapi/v1.json → labeling:8080/openapi/v1.json
    foreach (var service in services) o.SwaggerEndpoint($"/{service}/openapi/v1.json", service);
    o.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapReverseProxy();
app.Run();
