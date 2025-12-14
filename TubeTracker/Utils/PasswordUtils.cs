using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace TubeTracker.API.Utils;

public static class PasswordUtils
{
    // When using PBKDF2, NIST makes the following recommendations:
    // • A salt length of at least 128 bits (16 bytes)
    // • A hash function that is specified in FIPS 180-4 (e.g. SHA-256)
    // National Institute of Standards and Technology Computer Security Division (2010)
    // NIST Special Publication 800-132: Recommendation for Password-Based Key Derivation [online].
    // Available from: https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-132.pdf [Accessed 11 November 2025]

    // When using PBKDF2 with SHA256, OWASP makes the following recommendation for the iteration count:
    // • A minimum of 600,000 iterations
    // Open Worldwide Application Security Project (OWASP) Cheat Sheet Series (2024)
    // Password Storage Cheat Sheet [online].
    // Available from: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html [Accessed 11 November 2025]

    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const KeyDerivationPrf Prf = KeyDerivationPrf.HMACSHA256;
    private const int IterationCount = 600_000;

    // To prevent timing attacks, the code path should be the same whether the user is found or not.
    // The password verification will then fail, but it will have taken the same amount of time.
    private const string DummyPasswordSaltAndHash = "jk7PLv+C/Vzwxos1JITzLCvfdBi2E2NtplmwXvg15UhSHkPN/Iopn71HvJ88aM4I";

    public static string HashPasswordWithSalt(string password)
    {
        byte[] hash = HashPassword(password, out byte[] salt);

        byte[] saltAndHash = new byte[SaltLength + hash.Length];
        Buffer.BlockCopy(salt, 0, saltAndHash, 0, SaltLength);
        Buffer.BlockCopy(hash, 0, saltAndHash, SaltLength, hash.Length);

        return Convert.ToBase64String(saltAndHash);
    }

    public static bool VerifyPassword(string password, string? saltAndHash)
    {
        bool saltAndHashProvided = saltAndHash is not null;
        if (!saltAndHashProvided)
        {
            saltAndHash = DummyPasswordSaltAndHash;
        }

        byte[] convertedSaltAndHash;
        try
        {
            convertedSaltAndHash = Convert.FromBase64String(saltAndHash!);
        }
        catch (FormatException baseException)
        {
            throw new ArgumentException("Salt and Hash lengths are invalid", baseException);
        }

        // Ensure that the saltAndHash is of the correct length
        if (convertedSaltAndHash.Length != SaltLength + HashLength)
        {
            throw new ArgumentException("Salt and Hash lengths are invalid");
        }

        byte[] salt = new byte[SaltLength];
        Array.Copy(convertedSaltAndHash, 0, salt, 0, SaltLength);

        byte[] hash = new byte[HashLength];
        Array.Copy(convertedSaltAndHash, SaltLength, hash, 0, HashLength);

        byte[] hashedPassword = HashPassword(password, salt);

        // Do not use short-circuiting and operation to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(hashedPassword, hash) & saltAndHashProvided;
    }

    private static byte[] HashPassword(string password, out byte[] salt)
    {
        // The salt is a cryptographically secure byte array of the specified length
        salt = RandomNumberGenerator.GetBytes(SaltLength);
        return HashPassword(password, salt);
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        // The Pbkdf2 method is the recommended way to hash passwords in .NET
        return KeyDerivation.Pbkdf2(password, salt, Prf, IterationCount, HashLength);
    }
}
