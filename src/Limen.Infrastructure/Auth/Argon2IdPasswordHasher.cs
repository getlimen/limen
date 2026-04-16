using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Limen.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Limen.Infrastructure.Auth;

public sealed class Argon2IdPasswordHasher : IPasswordHasher
{
    private const int MemorySize = 65536; // 64 MiB
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 1;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    private readonly ILogger<Argon2IdPasswordHasher> _logger;

    public Argon2IdPasswordHasher(ILogger<Argon2IdPasswordHasher> logger)
    {
        _logger = logger;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeHash(password, salt);
        var saltB64 = Convert.ToBase64String(salt);
        var hashB64 = Convert.ToBase64String(hash);
        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltB64}${hashB64}";
    }

    public bool Verify(string password, string encodedHash)
    {
        try
        {
            // Parse PHC string: $argon2id$v=19$m=65536,t=3,p=1$<salt_b64>$<hash_b64>
            var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) { return false; }
            if (parts[0] != "argon2id") { return false; }
            // parts[1] = "v=19", parts[2] = "m=65536,t=3,p=1", parts[3] = salt_b64, parts[4] = hash_b64
            var paramParts = parts[2].Split(',');
            int m = 0, t = 0, p = 0;
            foreach (var param in paramParts)
            {
                if (param.StartsWith("m=", StringComparison.Ordinal))
                {
                    if (!int.TryParse(param[2..], out m))
                    {
                        _logger.LogWarning("Stored Argon2 hash is corrupt: {Reason}", $"cannot parse m parameter '{param}'");
                        return false;
                    }
                }
                else if (param.StartsWith("t=", StringComparison.Ordinal))
                {
                    if (!int.TryParse(param[2..], out t))
                    {
                        _logger.LogWarning("Stored Argon2 hash is corrupt: {Reason}", $"cannot parse t parameter '{param}'");
                        return false;
                    }
                }
                else if (param.StartsWith("p=", StringComparison.Ordinal))
                {
                    if (!int.TryParse(param[2..], out p))
                    {
                        _logger.LogWarning("Stored Argon2 hash is corrupt: {Reason}", $"cannot parse p parameter '{param}'");
                        return false;
                    }
                }
            }
            var salt = Convert.FromBase64String(parts[3]);
            var expectedHash = Convert.FromBase64String(parts[4]);
            var actualHash = ComputeHash(password, salt, m, t, p, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ComputeHash(string password, byte[] salt,
        int memorySize = MemorySize, int iterations = Iterations,
        int parallelism = DegreeOfParallelism, int hashLength = HashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = memorySize;
        argon2.Iterations = iterations;
        argon2.DegreeOfParallelism = parallelism;
        return argon2.GetBytes(hashLength);
    }
}
