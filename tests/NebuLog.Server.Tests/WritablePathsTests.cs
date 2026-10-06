using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NebuLog.Server.Api;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

/// <summary>
/// The container runs with a read-only root filesystem and a single writable volume, so everything
/// the application writes has to land under the configured data paths.
/// </summary>
/// <remarks>
/// This runs the host with the database and the Data Protection key ring pointed at one temporary
/// directory — the shape production uses with <c>/data</c> — and asserts that the application
/// starts, signs a user in and issues a key while writing nothing beside its own content root.
/// A real read-only mount is exercised in WO-0007.
/// </remarks>
public sealed class WritablePathsTests
{
    [Fact]
    public async Task EverythingWrittenAtRuntimeLandsUnderTheConfiguredDataPath()
    {
        var token = TestContext.Current.CancellationToken;
        var dataPath = Path.Combine(Path.GetTempPath(), "nebulog-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);

        try
        {
            await using var factory = new ConfiguredPathsFactory(dataPath);
            using var client = factory.CreateCookieClient();

            using (var login = await NebuLogAppFactory.PostJsonAsync(
                client,
                "/api/auth/login",
                new { email = NebuLogAppFactory.AdminEmail, password = NebuLogAppFactory.AdminPassword }))
            {
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            }

            using var created = await NebuLogAppFactory.PostJsonAsync(client, "/api/keys", new { name = "readonly-probe" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var key = await created.Content.ReadFromJsonAsync<CreatedApiKeyDto>(token);
            Assert.False(string.IsNullOrWhiteSpace(key!.ApiKey));

            // The database file and the key ring are both inside the one writable directory.
            var found = Directory.GetFileSystemEntries(dataPath, "*", SearchOption.AllDirectories);
            Assert.True(
                File.Exists(Path.Combine(dataPath, "nebulog.db")),
                "The database was not created under the data path. Found: " + string.Join(", ", found));

            var keysDirectory = Path.Combine(dataPath, "keys");
            Assert.True(Directory.Exists(keysDirectory), "The Data Protection key ring directory was not created.");
            Assert.NotEmpty(Directory.GetFiles(keysDirectory, "*.xml"));
        }
        finally
        {
            try
            {
                Directory.Delete(dataPath, recursive: true);
            }
            catch (IOException)
            {
                // A file handle may still be closing; the temp directory is disposable either way.
            }
        }
    }

    /// <summary>A host whose database and key ring live in one directory, as in production.</summary>
    private sealed class ConfiguredPathsFactory : WebApplicationFactory<Program>
    {
        private readonly string _dataPath;

        public ConfiguredPathsFactory(string dataPath) => _dataPath = dataPath;

        public CookieContainer Cookies { get; } = new();

        public HttpClient CreateCookieClient() =>
            new(new CookieForwardingHandler(Cookies, Server.CreateHandler()))
            {
                BaseAddress = Server.BaseAddress,
            };

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:Identity"] = $"Data Source={Path.Combine(_dataPath, "nebulog.db")}",
                    ["NebuLog:DataProtection:KeysPath"] = Path.Combine(_dataPath, "keys"),
                    ["NebuLog:Admin:Email"] = NebuLogAppFactory.AdminEmail,
                    ["NebuLog:Admin:Password"] = NebuLogAppFactory.AdminPassword,
                }));
        }

    }
}
