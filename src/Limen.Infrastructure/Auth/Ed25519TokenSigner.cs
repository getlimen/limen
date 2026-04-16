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
            _key = Key.Import(SignatureAlgorithm.Ed25519, bytes, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        }
        else
        {
            _key = Key.Create(SignatureAlgorithm.Ed25519,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            File.WriteAllBytes(keyPath, _key.Export(KeyBlobFormat.RawPrivateKey));
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

    public byte[] PublicKeyBytes => _key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

    public void Dispose() => _key.Dispose();

    private static string Base64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
