using System.Security.Cryptography;
using System.Text;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Mediator;

namespace Limen.Application.Commands.Nodes;

public sealed record CreateProvisioningKeyResult(Guid Id, string PlaintextKey, DateTimeOffset ExpiresAt);

public sealed record CreateProvisioningKeyCommand(string[] IntendedRoles) : ICommand<CreateProvisioningKeyResult>;

internal sealed class CreateProvisioningKeyCommandHandler : ICommandHandler<CreateProvisioningKeyCommand, CreateProvisioningKeyResult>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public CreateProvisioningKeyCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<CreateProvisioningKeyResult> Handle(CreateProvisioningKeyCommand cmd, CancellationToken ct)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
        var now = _clock.UtcNow;
        var pk = new ProvisioningKey
        {
            Id = Guid.NewGuid(),
            KeyHash = hash,
            IntendedRoles = cmd.IntendedRoles,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15),
        };
        _db.ProvisioningKeys.Add(pk);
        await _db.SaveChangesAsync(ct);
        return new CreateProvisioningKeyResult(pk.Id, plaintext, pk.ExpiresAt);
    }
}
