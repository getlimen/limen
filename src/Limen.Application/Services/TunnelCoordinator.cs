using Limen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using NSec.Cryptography;

namespace Limen.Application.Services;

public sealed class TunnelCoordinator : ITunnelCoordinator
{
    private readonly IAppDbContext _db;
    public TunnelCoordinator(IAppDbContext db) { _db = db; }

    public async Task<string> AllocateTunnelIpAsync(CancellationToken ct)
    {
        var used = await _db.WireGuardPeers
            .Where(p => p.RevokedAt == null)
            .Select(p => p.TunnelIp)
            .ToListAsync(ct);
        var usedSet = used
            .Select(ip => int.Parse(ip.Split('.')[3].Split('/')[0]))
            .ToHashSet();
        for (int i = 2; i <= 250; i++)
        {
            if (!usedSet.Contains(i))
            {
                return $"10.42.0.{i}/32";
            }
        }
        throw new InvalidOperationException("Subnet 10.42.0.0/24 exhausted");
    }

    public (string publicKeyBase64, string privateKeyBase64) GenerateKeypair()
    {
        using var key = Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        var priv = key.Export(KeyBlobFormat.RawPrivateKey);
        var pub = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        return (Convert.ToBase64String(pub), Convert.ToBase64String(priv));
    }
}
