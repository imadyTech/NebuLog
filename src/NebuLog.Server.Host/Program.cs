var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health/live");

// Client-side routes of the React dashboard fall back to index.html.
app.MapFallbackToFile("index.html");

await app.RunAsync();

/// <summary>Entry-point marker that allows integration tests to host the application.</summary>
public partial class Program
{
}
