using System.Security.Cryptography;
using System.Text;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Nodes;

public sealed record EnrollAgentResult(Guid AgentId, string Secret);

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

    public EnrollAgentCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

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
        _db.Nodes.Add(node);
        _db.Agents.Add(agent);
        pk.UsedAt = now;
        pk.ResultingNodeId = node.Id;
        await _db.SaveChangesAsync(ct);
        return new EnrollAgentResult(agent.Id, secret);
    }
}
