using System.Security.Cryptography;
using System.Text;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.AgentMessages;
using Limen.Domain.Nodes;
using Limen.Domain.Tunnels;
using Limen.Application.Common.Options;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Limen.Application.Commands.Nodes;

public sealed record EnrollAgentResult(Guid AgentId, string Secret, WireGuardConfig Wireguard);

public sealed record EnrollAgentCommand(
    string ProvisioningKeyPlaintext,
    string Hostname,
    string[] Roles,
    string Platform,
    string AgentVersion) : ICommand<EnrollAgentResult>;

internal sealed class EnrollAgentCommandHandler : ICommandHandler<EnrollAgentCommand, EnrollAgentResult>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly ITunnelCoordinator _tunnels;
    private readonly IForculusClient _forculus;
    private readonly IOptions<WgServerSettings> _wgServerSettings;
    private readonly ILogger<EnrollAgentCommandHandler> _log;

    public EnrollAgentCommandHandler(
        IAppDbContext db,
        IClock clock,
        ITunnelCoordinator tunnels,
        IForculusClient forculus,
        IOptions<WgServerSettings> wgServerSettings,
        ILogger<EnrollAgentCommandHandler> log)
    {
        _db = db;
        _clock = clock;
        _tunnels = tunnels;
        _forculus = forculus;
        _wgServerSettings = wgServerSettings;
        _log = log;
    }

    public async ValueTask<EnrollAgentResult> Handle(EnrollAgentCommand cmd, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cmd.ProvisioningKeyPlaintext)));
        var now = _clock.UtcNow;
        var pk = await _db.ProvisioningKeys.FirstOrDefaultAsync(x => x.KeyHash == hash, ct)
            ?? throw new InvalidOperationException("Invalid provisioning key.");
        if (pk.UsedAt is not null)
        {
            throw new InvalidOperationException("Provisioning key already used.");
        }
        if (pk.ExpiresAt < now)
        {
            throw new InvalidOperationException("Provisioning key expired.");
        }

        var node = new Node
        {
            Id = Guid.NewGuid(),
            Name = cmd.Hostname,
            Roles = cmd.Roles,
            Status = NodeStatus.Pending,
            CreatedAt = now,
        };
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            NodeId = node.Id,
            SecretHash = secretHash,
            AgentVersion = cmd.AgentVersion,
            Platform = cmd.Platform,
            Hostname = cmd.Hostname,
            EnrolledAt = now,
        };

        var tunnelIp = await _tunnels.AllocateTunnelIpAsync(ct);
        var (pubKey, privKey) = _tunnels.GenerateKeypair();
        var peer = new WireGuardPeer
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            PublicKey = pubKey,
            TunnelIp = tunnelIp,
            CreatedAt = now,
        };

        _db.Nodes.Add(node);
        _db.Agents.Add(agent);
        _db.WireGuardPeers.Add(peer);
        pk.UsedAt = now;
        pk.ResultingNodeId = node.Id;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _forculus.UpsertPeerAsync(new Limen.Contracts.ForculusHttp.PeerSpec(pubKey, tunnelIp), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to push peer {Key} to Forculus at enroll time; reconcile loop will eventually sync it", pubKey);
        }

        var wg = new WireGuardConfig(
            InterfaceAddress: tunnelIp,
            PrivateKey: privKey,
            ServerPublicKey: _wgServerSettings.Value.PublicKey,
            ServerEndpoint: _wgServerSettings.Value.Endpoint,
            KeepaliveSeconds: 25);

        return new EnrollAgentResult(agent.Id, secret, wg);
    }
}
