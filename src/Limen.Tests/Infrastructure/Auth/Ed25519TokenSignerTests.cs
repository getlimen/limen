using FluentAssertions;
using Limen.Infrastructure.Auth;
using NSec.Cryptography;

namespace Limen.Tests.Infrastructure.Auth;

public sealed class Ed25519TokenSignerTests : IDisposable
{
    private readonly string _keyPath = Path.Combine(Path.GetTempPath(), $"test-key-{Guid.NewGuid()}.bin");

    public void Dispose()
    {
        if (File.Exists(_keyPath))
        {
            File.Delete(_keyPath);
        }
    }

    [Fact]
    public void Sign_and_verify_round_trip()
    {
        using var signer = new Ed25519TokenSigner(_keyPath, "test-kid");

        var header = new Dictionary<string, object> { ["alg"] = "EdDSA", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object> { ["sub"] = "test", ["exp"] = 9999999999L };

        var jwt = signer.SignJwt(header, payload);

        var parts = jwt.Split('.');
        parts.Should().HaveCount(3);

        // Reconstruct signing input
        var signingInput = System.Text.Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        var sigBytes = Base64UrlDecode(parts[2]);

        var pubKeyBytes = signer.PublicKeyBytes;
        var pubKey = PublicKey.Import(
            SignatureAlgorithm.Ed25519,
            pubKeyBytes,
            KeyBlobFormat.RawPublicKey);

        var isValid = SignatureAlgorithm.Ed25519.Verify(pubKey, signingInput, sigBytes);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void KeyId_is_set_correctly()
    {
        using var signer = new Ed25519TokenSigner(_keyPath, "my-key-id");
        signer.KeyId.Should().Be("my-key-id");
    }

    [Fact]
    public void Key_is_persisted_and_reloaded()
    {
        byte[] publicKeyBytesFirst;
        using (var signer1 = new Ed25519TokenSigner(_keyPath, "kid"))
        {
            publicKeyBytesFirst = signer1.PublicKeyBytes;
        }

        using var signer2 = new Ed25519TokenSigner(_keyPath, "kid");
        signer2.PublicKeyBytes.Should().Equal(publicKeyBytesFirst);
    }

    [Fact]
    public void Verify_AcceptsValidJwt()
    {
        using var signer = new Ed25519TokenSigner(_keyPath, "test-kid");

        var header = new Dictionary<string, object> { ["alg"] = "EdDSA", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object> { ["sub"] = "user@example.com", ["exp"] = 9999999999L };

        var jwt = signer.SignJwt(header, payload);

        signer.Verify(jwt).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsTamperedPayload()
    {
        using var signer = new Ed25519TokenSigner(_keyPath, "test-kid");

        var header = new Dictionary<string, object> { ["alg"] = "EdDSA", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object> { ["sub"] = "user@example.com", ["exp"] = 9999999999L };

        var jwt = signer.SignJwt(header, payload);
        var parts = jwt.Split('.');
        var tamperedPayload = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes("""{"sub":"attacker@x.com"}"""));
        var tampered = $"{parts[0]}.{tamperedPayload}.{parts[2]}";

        signer.Verify(tampered).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsMalformedJwt()
    {
        using var signer = new Ed25519TokenSigner(_keyPath, "test-kid");

        signer.Verify("not.a.valid.jwt.at.all").Should().BeFalse();
        signer.Verify("only.two").Should().BeFalse();
        signer.Verify(string.Empty).Should().BeFalse();
    }

    private static string Base64UrlEncode(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        var mod = padded.Length % 4;
        if (mod == 2) { padded += "=="; }
        else if (mod == 3) { padded += "="; }
        return Convert.FromBase64String(padded);
    }
}
