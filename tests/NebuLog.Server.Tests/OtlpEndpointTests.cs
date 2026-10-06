using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Microsoft.AspNetCore.SignalR.Client;
using NebuLog.Contracts;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class OtlpEndpointTests
{
    private const string ProtobufContentType = "application/x-protobuf";
    private const string JsonContentType = "application/json";

    private static ExportLogsServiceRequest SampleRequest(string body = "otlp hello") => new()
    {
        ResourceLogs =
        {
            new ResourceLogs
            {
                Resource = new Resource
                {
                    Attributes =
                    {
                        new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "OrderApi" } },
                        new KeyValue { Key = "service.instance.id", Value = new AnyValue { StringValue = "i-7" } },
                    },
                },
                ScopeLogs =
                {
                    new ScopeLogs
                    {
                        Scope = new InstrumentationScope { Name = "Orders.Checkout" },
                        LogRecords =
                        {
                            new LogRecord
                            {
                                TimeUnixNano = 1_700_000_000_000_000_000,
                                SeverityNumber = SeverityNumber.Error,
                                SeverityText = "Error",
                                Body = new AnyValue { StringValue = body },
                                Attributes =
                                {
                                    new KeyValue { Key = "order.id", Value = new AnyValue { IntValue = 42 } },
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    private static HttpContent ProtobufContent(ExportLogsServiceRequest request)
    {
        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);
        return content;
    }

    private static HttpContent JsonContent(string json)
    {
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
        return content;
    }

    [Fact]
    public async Task ProtobufExportIsAcceptedAndReachesViewers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();

        var received = new TaskCompletionSource<IReadOnlyList<NebuLogEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<IReadOnlyList<NebuLogEntry>>(HubRoutes.ReceiveLogs, batch => received.TrySetResult(batch));

        using var client = await factory.CreateProducerClientAsync();
        using var response = await client.PostAsync(
            new Uri("/v1/logs", UriKind.Relative), ProtobufContent(SampleRequest()), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ProtobufContentType, response.Content.Headers.ContentType?.MediaType);

        // An empty ExportLogsServiceResponse means "everything accepted".
        var payload = await response.Content.ReadAsByteArrayAsync(token);
        var parsed = ExportLogsServiceResponse.Parser.ParseFrom(payload);
        Assert.Null(parsed.PartialSuccess);

        var batch = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        var entry = Assert.Single(batch);
        Assert.Equal("otlp hello", entry.Body);
        Assert.Equal("OrderApi", entry.ServiceName);
        Assert.Equal("i-7", entry.ServiceInstanceId);
        Assert.Equal("Orders.Checkout", entry.ScopeName);
        Assert.Equal(Severity.Error, entry.SeverityNumber);
        Assert.Equal("42", entry.Attributes["order.id"]);
        Assert.Equal(IngestSource.Otlp, entry.Source);
    }

    [Fact]
    public async Task JsonExportIsAccepted()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();

        var received = new TaskCompletionSource<IReadOnlyList<NebuLogEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<IReadOnlyList<NebuLogEntry>>(HubRoutes.ReceiveLogs, batch => received.TrySetResult(batch));

        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "otlp-logs.json"), token);

        using var client = await factory.CreateProducerClientAsync();
        using var response = await client.PostAsync(new Uri("/v1/logs", UriKind.Relative), JsonContent(json), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonContentType, response.Content.Headers.ContentType?.MediaType);

        var batch = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal(2, batch.Count);

        var first = batch[0];
        Assert.Equal("checkout started", first.Body);
        Assert.Equal("CartApi", first.ServiceName);
        Assert.Equal("Cart.Checkout", first.ScopeName);
        Assert.Equal(Severity.Info, first.SeverityNumber);
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", first.TraceId);
        Assert.Equal("00f067aa0ba902b7", first.SpanId);
        Assert.Equal("9007199254740993", first.Attributes["big.number"]);
        Assert.Equal("true", first.Attributes["cart.empty"]);
        Assert.Equal("[\"a\",\"b\"]", first.Attributes["tags"]);

        var second = batch[1];
        Assert.Equal(Severity.Error, second.SeverityNumber);
        Assert.Null(second.TraceId);
        Assert.NotNull(second.Exception);
        Assert.Equal("System.TimeoutException", second.Exception.Type);
        Assert.Equal("gateway did not respond", second.Exception.Message);
    }

    [Fact]
    public async Task GzipEncodedBodiesAreAccepted()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.CreateProducerClientAsync();

        using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var raw = SampleRequest("gzipped").ToByteArray();
            await gzip.WriteAsync(raw, token);
        }

        using var content = new ByteArrayContent(compressed.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);
        content.Headers.ContentEncoding.Add("gzip");

        using var response = await client.PostAsync(new Uri("/v1/logs", UriKind.Relative), content, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedContentTypeReturns415()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.CreateProducerClientAsync();

        using var content = new StringContent("<logs/>", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

        using var response = await client.PostAsync(new Uri("/v1/logs", UriKind.Relative), content, token);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task OversizedBodyReturns413()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Otlp:MaxRequestBodyBytes"] = "2048",
        });
        using var client = await factory.CreateProducerClientAsync();

        using var response = await client.PostAsync(
            new Uri("/v1/logs", UriKind.Relative),
            ProtobufContent(SampleRequest(new string('x', 8_000))),
            token);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task MalformedProtobufReturns400WithARpcStatus()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.CreateProducerClientAsync();

        using var content = new ByteArrayContent([0xff, 0xff, 0xff, 0xff, 0x0f]);
        content.Headers.ContentType = new MediaTypeHeaderValue(ProtobufContentType);

        using var response = await client.PostAsync(new Uri("/v1/logs", UriKind.Relative), content, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ProtobufContentType, response.Content.Headers.ContentType?.MediaType);

        var status = Google.Rpc.Status.Parser.ParseFrom(await response.Content.ReadAsByteArrayAsync(token));
        Assert.Equal(3, status.Code);
        Assert.False(string.IsNullOrWhiteSpace(status.Message));
    }

    [Fact]
    public async Task MalformedJsonReturns400WithAJsonStatus()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.CreateProducerClientAsync();

        using var response = await client.PostAsync(
            new Uri("/v1/logs", UriKind.Relative), JsonContent("{\"resourceLogs\": \"not-an-array\"}"), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(JsonContentType, response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Equal(3, document.RootElement.GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task AFullIngestQueueReturns503WithRetryAfter()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            // The smallest queue the options allow, with the pipeline held back by a slow broadcast
            // interval, so a modest export is guaranteed to overflow it.
            ["NebuLog:Server:IngestQueueCapacity"] = "1000",
            ["NebuLog:Server:BroadcastInterval"] = "00:00:02",
            ["NebuLog:Otlp:MaxRequestBodyBytes"] = "67108864",
            ["NebuLog:RateLimit:IngestBucketCapacity"] = "100000",
            ["NebuLog:RateLimit:IngestTokensPerSecond"] = "100000",
        });
        using var client = await factory.CreateProducerClientAsync();

        var flood = new ExportLogsServiceRequest();
        var scope = new ScopeLogs { Scope = new InstrumentationScope { Name = "flood" } };
        for (var i = 0; i < 4_000; i++)
        {
            scope.LogRecords.Add(new LogRecord { Body = new AnyValue { StringValue = "x" } });
        }

        flood.ResourceLogs.Add(new ResourceLogs { ScopeLogs = { scope } });

        HttpStatusCode? last = null;
        for (var attempt = 0; attempt < 3 && last != HttpStatusCode.ServiceUnavailable; attempt++)
        {
            using var response = await client.PostAsync(
                new Uri("/v1/logs", UriKind.Relative), ProtobufContent(flood), token);
            last = response.StatusCode;

            if (last == HttpStatusCode.ServiceUnavailable)
            {
                Assert.Equal(1, response.Headers.RetryAfter?.Delta?.TotalSeconds);
            }
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, last);
    }

    [Fact]
    public async Task RateLimitingReturns429()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:RateLimit:IngestBucketCapacity"] = "2",
            ["NebuLog:RateLimit:IngestTokensPerSecond"] = "1",
        });
        using var client = await factory.CreateProducerClientAsync();

        HttpStatusCode? last = null;
        for (var i = 0; i < 5; i++)
        {
            using var response = await client.PostAsync(
                new Uri("/v1/logs", UriKind.Relative), ProtobufContent(SampleRequest()), token);
            last = response.StatusCode;

            if (last == HttpStatusCode.TooManyRequests)
            {
                Assert.NotNull(response.Headers.RetryAfter);
                break;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task CorsPreflightAllowsAConfiguredOrigin()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Cors:AllowedOrigins"] = "https://nebulog.imady.co.nz, https://dash.example.test",
        });
        using var client = await factory.CreateProducerClientAsync();

        using var allowed = await SendPreflightAsync(client, "https://dash.example.test", token);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal(
            "https://dash.example.test",
            Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));

        using var denied = await SendPreflightAsync(client, "https://evil.example.test", token);
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task CorsAllowsNothingWhenNoOriginsAreConfigured()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.CreateProducerClientAsync();

        using var response = await SendPreflightAsync(client, "https://dash.example.test", token);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task<HttpResponseMessage> SendPreflightAsync(
        HttpClient client,
        string origin,
        CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/v1/logs", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-api-key");

        return await client.SendAsync(request, token);
    }
}
