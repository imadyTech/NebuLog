using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NebuLog.Server;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class ServerOptionsValidationTests
{
    /// <summary>A minimal environment so the options under test can be resolved without a host.</summary>
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NebuLog.Tests";

        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "nebulog-tests");

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static NebuLogServerOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNebuLogServer(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<NebuLogServerOptions>>().Value;
    }

    [Fact]
    public void BindsTheConfiguredSection()
    {
        var options = Resolve(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Server:BufferCapacity"] = "2000",
            ["NebuLog:Server:BroadcastInterval"] = "00:00:00.250",
        });

        Assert.Equal(2_000, options.BufferCapacity);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.BroadcastInterval);
    }

    [Theory]
    [InlineData("NebuLog:Server:BufferCapacity", "999")]
    [InlineData("NebuLog:Server:BufferCapacity", "2000000")]
    [InlineData("NebuLog:Server:IngestQueueCapacity", "999")]
    [InlineData("NebuLog:Server:MaxBroadcastBatch", "0")]
    [InlineData("NebuLog:Server:MaxBroadcastBatch", "20000")]
    [InlineData("NebuLog:Server:MaxAttributeValueLength", "4")]
    public void RejectsValuesOutsideTheDocumentedRanges(string key, string value) =>
        Assert.Throws<OptionsValidationException>(() => Resolve(
            new Dictionary<string, string?>(StringComparer.Ordinal) { [key] = value }));

    [Theory]
    [InlineData("00:00:00.010")]
    [InlineData("00:00:03")]
    public void RejectsBroadcastIntervalsOutsideTheWindow(string interval) =>
        Assert.Throws<OptionsValidationException>(() => Resolve(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["NebuLog:Server:BroadcastInterval"] = interval,
            }));

    [Fact]
    public void RejectsABroadcastBatchLargerThanTheBuffer() =>
        Assert.Throws<OptionsValidationException>(() => Resolve(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["NebuLog:Server:BufferCapacity"] = "1000",
                ["NebuLog:Server:MaxBroadcastBatch"] = "5000",
            }));

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("172.30.20.0/24", 1)]
    [InlineData("172.30.20.0/24, 10.0.0.0/8", 2)]
    [InlineData("not-a-network, 172.30.20.0/24, 10.0.0.0/notanumber", 1)]
    public void ParsesOnlyWellFormedCidrBlocks(string? configured, int expected) =>
        Assert.Equal(
            expected,
            NebuLogServerServiceCollectionExtensions.ParseNetworks(configured).Count());
}
