using NebuLog.Server.Host;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNebuLogServer(builder.Configuration);
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

/// <summary>Entry-point marker that allows integration tests to host the application.</summary>
public partial class Program
{
}
