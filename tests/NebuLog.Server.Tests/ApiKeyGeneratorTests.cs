using System.Security.Cryptography;
using System.Text;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class ApiKeyGeneratorTests
{
    [Fact]
    public void GeneratesKeysInTheDocumentedShape()
    {
        var generated = ApiKeyGenerator.Generate();

        var parts = generated.ClearText.Split('_');
        Assert.Equal(3, parts.Length);
        Assert.Equal(ApiKeyGenerator.Scheme, parts[0]);
        Assert.Equal(ApiKeyGenerator.PrefixLength, parts[1].Length);
        Assert.Equal(ApiKeyGenerator.SecretLength, parts[2].Length);
        Assert.Equal(parts[1], generated.Prefix);
        Assert.All(parts[1] + parts[2], character => Assert.True(char.IsAsciiLetterOrDigit(character)));
    }

    [Fact]
    public void StoresOnlyAHashOfTheSecret()
    {
        var generated = ApiKeyGenerator.Generate();
        var secret = generated.ClearText.Split('_')[2];

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(secret)), generated.SecretHash);

        // The clear text must not be recoverable from what is stored.
        Assert.DoesNotContain(
            Encoding.UTF8.GetString(generated.SecretHash.Select(b => (char)b).Select(c => (byte)c).ToArray()),
            generated.ClearText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratesDistinctKeys()
    {
        var keys = Enumerable.Range(0, 200).Select(_ => ApiKeyGenerator.Generate().ClearText).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RoundTripsAGeneratedKey()
    {
        var generated = ApiKeyGenerator.Generate();

        Assert.True(ApiKeyGenerator.TryParse(generated.ClearText, out var parsed));
        Assert.Equal(generated.Prefix, parsed.Prefix);
        Assert.True(ApiKeyGenerator.SecretMatches(parsed.Secret, generated.SecretHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-key")]
    [InlineData("nbl_short_0123456789abcdef0123456789abcdef")]
    [InlineData("nbl_waytoolongprefix_0123456789abcdef0123456789abcdef")]
    [InlineData("xyz_abcdefgh_0123456789abcdef0123456789abcdef")]
    [InlineData("nbl_abcdefgh_short")]
    [InlineData("nbl_abcdefgh")]
    [InlineData("nbl_abcdef!h_0123456789abcdef0123456789abcdef")]
    public void RejectsMalformedKeys(string? value) =>
        Assert.False(ApiKeyGenerator.TryParse(value, out _));

    [Fact]
    public void SecretMatchingRejectsTheWrongSecret()
    {
        var generated = ApiKeyGenerator.Generate();
        var other = ApiKeyGenerator.Generate().ClearText.Split('_')[2];

        Assert.False(ApiKeyGenerator.SecretMatches(other, generated.SecretHash));
        Assert.False(ApiKeyGenerator.SecretMatches(string.Empty, generated.SecretHash));
    }
}
