using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NebuLog.Contracts;
using NebuLog.Server.Api;
using NebuLog.Server.Identity;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class AuthorizationTests
{
    private static Uri Relative(string path) => new(path, UriKind.Relative);

    private static HttpContent SampleExport()
    {
        var request = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    ScopeLogs =
                    {
                        new ScopeLogs
                        {
                            Scope = new InstrumentationScope { Name = "auth" },
                            LogRecords = { new LogRecord { Body = new AnyValue { StringValue = "hello" } } },
                        },
                    },
                },
            },
        };

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
        return content;
    }

    [Fact]
    public async Task AnonymousHubConnectionsAreRejected()
    {
        await using var factory = new NebuLogAppFactory();

        await Assert.ThrowsAnyAsync<Exception>(() => factory.ConnectAnonymouslyAsync());
    }

    [Fact]
    public async Task AViewerCannotPublishLogs()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();

        var error = await Assert.ThrowsAsync<HubException>(() => viewer.InvokeAsync(
            HubRoutes.PublishLogs,
            new[] { new NebuLogEntry { Body = "nope" } },
            TestContext.Current.CancellationToken));

        Assert.Contains("unauthorized", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AProducerCannotReplayHistory()
    {
        await using var factory = new NebuLogAppFactory();
        await using var producer = await factory.ConnectProducerAsync("orders");

        await Assert.ThrowsAsync<HubException>(async () =>
        {
            var stream = producer.StreamAsync<NebuLogEntry>(
                HubRoutes.StreamHistory, new HistoryQuery(), TestContext.Current.CancellationToken);

            await foreach (var _ in stream.WithCancellation(TestContext.Current.CancellationToken))
            {
                // The first read is what surfaces the authorisation failure.
            }
        });
    }

    [Fact]
    public async Task OnlyAnOperatorCanSendCommands()
    {
        await using var factory = new NebuLogAppFactory();
        await using var producer = await factory.ConnectProducerAsync("orders");

        await using (var viewer = await factory.ConnectViewerAsync())
        {
            var refused = await Assert.ThrowsAsync<HubException>(() => viewer.InvokeAsync(
                HubRoutes.SendCommand,
                producer.ConnectionId,
                new NebuLogCommand { Name = "flush" },
                TestContext.Current.CancellationToken));

            Assert.Contains("unauthorized", refused.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var @operator = await factory.ConnectViewerAsync(NebuLogRoles.Operator);

        var delivered = new TaskCompletionSource<NebuLogCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.On<NebuLogCommand>(HubRoutes.ReceiveCommand, command => delivered.TrySetResult(command));

        await @operator.InvokeAsync(
            HubRoutes.SendCommand,
            producer.ConnectionId,
            new NebuLogCommand { Name = "flush" },
            TestContext.Current.CancellationToken);

        var received = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("flush", received.Name);
    }

    [Fact]
    public async Task AProducerIsRegisteredAsAProducerWithoutAnyQueryParameter()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();

        var announced = new TaskCompletionSource<IReadOnlyList<ConnectedClientInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<IReadOnlyList<ConnectedClientInfo>>(HubRoutes.ClientsChanged, clients =>
        {
            if (clients.Any(client => client.Kind == "producer"))
            {
                announced.TrySetResult(clients);
            }
        });

        // The key pins the service name, so the client registry learns it from the identity.
        var (_, apiKey) = await factory.IssueApiKeyAsync("orders-key", "orders");
        await using var producer = await factory.ConnectAsProducerAsync(apiKey);

        var clients = await announced.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var registered = Assert.Single(clients, client => client.Kind == "producer");
        Assert.Equal("orders", registered.ServiceName);
    }

    [Fact]
    public async Task OtlpRejectsCallersWithoutAKey()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await client.PostAsync(
            Relative("/v1/logs"), SampleExport(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OtlpRejectsACookieSessionThatIsNotAProducer()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Admin);

        using var response = await client.PostAsync(
            Relative("/v1/logs"), SampleExport(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtlpRejectsAnUnknownKey()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateApiKeyClient("nbl_abcdefgh_0123456789abcdef0123456789abcdef");

        using var response = await client.PostAsync(
            Relative("/v1/logs"), SampleExport(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ANewKeyWorksImmediatelyAndStopsWorkingWhenRevoked()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var admin = await factory.SignInAsAsync(NebuLogRoles.Admin);

        // Issue through the admin endpoint, so the whole round trip is covered.
        using var createResponse = await NebuLogAppFactory.PostJsonAsync(
            admin, "/api/keys", new { name = "ci-producer", serviceName = "ci" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedApiKeyDto>(token);
        Assert.False(string.IsNullOrWhiteSpace(created!.ApiKey));

        using var producer = factory.CreateApiKeyClient(created.ApiKey);
        using (var accepted = await producer.PostAsync(Relative("/v1/logs"), SampleExport(), token))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        using (var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, Relative($"/api/keys/{created.Id}")))
        {
            deleteRequest.Headers.Add(
                NebuLog.Server.Authentication.CsrfHeaderFilter.HeaderName,
                NebuLog.Server.Authentication.CsrfHeaderFilter.HeaderValue);

            using var revoked = await admin.SendAsync(deleteRequest, token);
            Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        }

        // Revocation evicts the cache entry, so the key must fail on the very next request.
        using var afterRevoke = await producer.PostAsync(Relative("/v1/logs"), SampleExport(), token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task ListedKeysNeverCarrySecretMaterial()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var admin = await factory.SignInAsAsync(NebuLogRoles.Admin);

        using var createResponse = await NebuLogAppFactory.PostJsonAsync(admin, "/api/keys", new { name = "listed" });
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedApiKeyDto>(token);

        var body = await admin.GetStringAsync(Relative("/api/keys"), token);

        Assert.Contains("listed", body, StringComparison.Ordinal);
        Assert.Contains(created!.Prefix, body, StringComparison.Ordinal);
        Assert.DoesNotContain(created.ApiKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("secretHash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatingAKeyRequiresAName()
    {
        await using var factory = new NebuLogAppFactory();
        using var admin = await factory.SignInAsAsync(NebuLogRoles.Admin);

        using var response = await NebuLogAppFactory.PostJsonAsync(admin, "/api/keys", new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheSeededDemoProducerKeyIsAccepted()
    {
        var seeded = ApiKeyGenerator.Generate().ClearText;
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:DemoProducer:ApiKey"] = seeded,
        });

        using var client = factory.CreateApiKeyClient(seeded);
        using var response = await client.PostAsync(
            Relative("/v1/logs"), SampleExport(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
