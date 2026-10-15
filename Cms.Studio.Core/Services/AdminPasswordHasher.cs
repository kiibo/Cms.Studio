using System.Security.Cryptography;
using System.Text;

namespace Cms.Studio.Core.Services;

/// <summary>
/// PBKDF2-SHA256 password hashing for the admin account.
/// Stored format: <c>PBKDF2${iterations}${saltBase64}${hashBase64}</c>.
/// </summary>
public static class AdminPasswordHasher
{
    public const string Prefix = "PBKDF2";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MinIterations = 10_000;
    private const int MaxIterations = 1_000_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = (stored ?? string.Empty).Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations is < MinIterations or > MaxIterations)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password ?? string.Empty, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Timing-safe string comparison (lengths are not compared in constant time).</summary>
    public static bool FixedEquals(string? a, string? b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a ?? string.Empty),
            Encoding.UTF8.GetBytes(b ?? string.Empty));
}
