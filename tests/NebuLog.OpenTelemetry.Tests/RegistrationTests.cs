using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace NebuLog.OpenTelemetry.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public async Task ComposesExporterRedactionAndClientRegistrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("OrderApi"))
            .WithLogging(logging => logging
                .AddNebuLogRedaction()
                .AddNebuLogExporter(options =>
                {
                    options.Endpoint = new Uri("https://logs.example.test");
                    options.ApiKey = "api-key";
                }));
        services.AddNebuLogClient();

        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<LoggerProvider>());
        Assert.NotNull(provider.GetRequiredService<INebuLogStats>());
        Assert.Same(
            provider.GetRequiredService<INebuLogStats>(),
            provider.GetRequiredService<INebuLogCommands>());
    }

    [Fact]
    public async Task ExporterWithoutClientRegistrationFailsWithAClearMessage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenTelemetry()
            .WithLogging(logging => logging.AddNebuLogExporter(options =>
            {
                options.Endpoint = new Uri("https://logs.example.test");
                options.ApiKey = "api-key";
            }));

        await using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<LoggerProvider>());
        Assert.Contains("AddNebuLogClient", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientOptionsAreValidated()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNebuLogClient(options => options.ApiKey = "api-key");

        await using var provider = services.BuildServiceProvider();

        Assert.Throws<global::Microsoft.Extensions.Options.OptionsValidationException>(
            () => provider.GetRequiredService<NebuLogClientRegistry>().BuildOptions());
    }
}
