using System.Security.Cryptography;
using System.Text;

namespace NebuLog.Server.Identity;

/// <summary>
/// Creates and parses API keys of the form <c>nbl_{prefix8}_{secret32}</c>.
/// </summary>
/// <remarks>
/// Both halves are base62 drawn from <see cref="RandomNumberGenerator"/>. The prefix is stored in
/// clear so a key can be looked up without scanning; the secret is only ever stored as a SHA-256
/// hash and compared with <see cref="CryptographicOperations.FixedTimeEquals"/>.
/// </remarks>
public static class ApiKeyGenerator
{
    /// <summary>The literal prefix every NebuLog key starts with.</summary>
    public const string Scheme = "nbl";

    /// <summary>Length of the lookup prefix.</summary>
    public const int PrefixLength = 8;

    /// <summary>Length of the secret.</summary>
    public const int SecretLength = 32;

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>Generates a new key.</summary>
    /// <returns>The clear-text key, its prefix and the hash to store.</returns>
    public static GeneratedApiKey Generate()
    {
        var prefix = RandomBase62(PrefixLength);
        var secret = RandomBase62(SecretLength);

        return new GeneratedApiKey(
            $"{Scheme}_{prefix}_{secret}",
            prefix,
            HashSecret(secret));
    }

    /// <summary>Splits a clear-text key into its prefix and secret.</summary>
    /// <param name="value">The key as presented by the client.</param>
    /// <param name="parsed">The parsed parts, when the format is valid.</param>
    /// <returns>True when <paramref name="value"/> is a well-formed NebuLog key.</returns>
    public static bool TryParse(string? value, out ParsedApiKey parsed)
    {
        parsed = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('_');
        if (parts.Length != 3 ||
            !string.Equals(parts[0], Scheme, StringComparison.Ordinal) ||
            parts[1].Length != PrefixLength ||
            parts[2].Length != SecretLength ||
            !IsBase62(parts[1]) ||
            !IsBase62(parts[2]))
        {
            return false;
        }

        parsed = new ParsedApiKey(parts[1], parts[2]);
        return true;
    }

    /// <summary>Hashes the secret half of a key.</summary>
    /// <param name="secret">The secret as presented by the client.</param>
    public static byte[] HashSecret(string secret) => SHA256.HashData(Encoding.UTF8.GetBytes(secret));

    /// <summary>Compares a presented secret against a stored hash without leaking timing.</summary>
    /// <param name="secret">The secret as presented by the client.</param>
    /// <param name="storedHash">The hash held in the database.</param>
    public static bool SecretMatches(string secret, byte[] storedHash) =>
        CryptographicOperations.FixedTimeEquals(HashSecret(secret), storedHash);

    private static string RandomBase62(int length)
    {
        var buffer = new char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(buffer);
    }

    private static bool IsBase62(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>A freshly generated key: the clear text is returned once and never stored.</summary>
/// <param name="ClearText">The full key to hand to the operator.</param>
/// <param name="Prefix">The lookup prefix to store.</param>
/// <param name="SecretHash">The hash to store.</param>
public sealed record GeneratedApiKey(string ClearText, string Prefix, byte[] SecretHash);

/// <summary>The two halves of a presented key.</summary>
/// <param name="Prefix">The lookup prefix.</param>
/// <param name="Secret">The secret to verify.</param>
public readonly record struct ParsedApiKey(string Prefix, string Secret);
