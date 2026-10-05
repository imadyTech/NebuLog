using NebuLog.Server.Host;
using Scalar.AspNetCore;

// The container's HEALTHCHECK runs this same executable with --health-check; see HealthProbe.
if (args.Contains(HealthProbe.Argument))
{
    return await HealthProbe.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNebuLogServer(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi();
builder.AddHostObservability();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapNebuLog();
app.MapNebuLogDashboardFallback();

await app.RunAsync();
return 0;

/// <summary>Entry-point marker that allows integration tests to host the application.</summary>
public partial class Program
{
}
