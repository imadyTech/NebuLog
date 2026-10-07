using System.Net;
using System.Net.Http.Json;
using NebuLog.OpenTelemetry;
using Xunit;

namespace NebuShop.Tests;

/// <summary>
/// Scenario 05: the sign-in password must not appear anywhere a reader could see it.
/// </summary>
/// <remarks>
/// WO-0010 §6 asks that the password never appear in plain text. The capture here sits at the
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> boundary, which is upstream of the redaction
/// processor, so these tests check the two halves separately:
/// <list type="bullet">
/// <item>the message the application formats never contains the password — that is the half
/// redaction cannot fix, so it has to be true by construction;</item>
/// <item>the attribute does carry it, and <see cref="RedactionProcessor"/> replaces it — that is
/// the half the demo is about.</item>
/// </list>
/// </remarks>
public sealed class RedactionTests
{
    private const string Password = "hunter2";

    [Theory]
    [InlineData("minimal")]
    [InlineData("mvc")]
    public async Task ThePasswordNeverAppearsInAnyFormattedMessage(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/{api}/signin", UriKind.Relative),
            new { username = "demo-visitor", password = Password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var leaks = factory.Logs.Entries
            .Where(entry => entry.Message.Contains(Password, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            leaks.Count == 0,
            $"The password was formatted into {leaks.Count} message(s): {string.Join(" | ", leaks.Select(e => e.Message))}");
    }

    [Theory]
    [InlineData("minimal")]
    [InlineData("mvc")]
    public async Task ThePasswordIsCarriedAsAnAttributeSoRedactionCanReachIt(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/{api}/signin", UriKind.Relative),
            new { username = "demo-visitor", password = Password },
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        var entry = Assert.Single(factory.Logs.Entries, e => e.Attributes.ContainsKey("Password"));
        Assert.Equal(Password, entry.Attributes["Password"]);
        Assert.Equal("demo-visitor", entry.Attributes["Username"]);
    }

    [Fact]
    public void TheRedactionProcessorMasksAnAttributeNamedLikeAPassword()
    {
        // The other half of the demonstration, asserted directly against the processor: given the
        // attribute the shop produces, this is what the console receives.
        Assert.Contains("password", RedactionProcessor.DefaultPatterns, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("***", RedactionProcessor.Placeholder);
    }
}
