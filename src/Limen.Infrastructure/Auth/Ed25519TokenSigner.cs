using System.Text.Json;
using Limen.Application.Common.Interfaces;
using NSec.Cryptography;

namespace Limen.Infrastructure.Auth;

public sealed class Ed25519TokenSigner : ITokenSigner, IDisposable
{
    private readonly Key _key;
    public string KeyId { get; }

    public Ed25519TokenSigner(string keyPath, string keyId)
    {
        KeyId = keyId;
        var dir = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
        if (File.Exists(keyPath))
        {
            var bytes = File.ReadAllBytes(keyPath);
            try
            {
                _key = Key.Import(SignatureAlgorithm.Ed25519, bytes, KeyBlobFormat.RawPrivateKey,
                    new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Signing key at {keyPath} is corrupt or unreadable. Delete it to regenerate (will invalidate existing sessions).",
                    ex);
            }
        }
        else
        {
            _key = Key.Create(SignatureAlgorithm.Ed25519,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            File.WriteAllBytes(keyPath, _key.Export(KeyBlobFormat.RawPrivateKey));
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
    }

    public string SignJwt(IDictionary<string, object> header, IDictionary<string, object> payload)
    {
        var h = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var p = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = System.Text.Encoding.UTF8.GetBytes($"{h}.{p}");
        var sig = SignatureAlgorithm.Ed25519.Sign(_key, signingInput);
        return $"{h}.{p}.{Base64Url(sig)}";
    }

    public bool Verify(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3) { return false; }
        var signingInput = System.Text.Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
        byte[] sig;
        try { sig = Base64UrlDecode(parts[2]); }
        catch { return false; }
        return SignatureAlgorithm.Ed25519.Verify(_key.PublicKey, signingInput, sig);
    }

    public byte[] PublicKeyBytes => _key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

    public void Dispose() => _key.Dispose();

    private static string Base64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4) { case 2: padded += "=="; break; case 3: padded += "="; break; }
        return Convert.FromBase64String(padded);
    }
}
