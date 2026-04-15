using Limen.Contracts.ForculusHttp;

namespace Limen.Application.Common.Interfaces;

public interface IForculusClient
{
    Task UpsertPeerAsync(PeerSpec peer, CancellationToken ct);
    Task RemovePeerAsync(string publicKey, CancellationToken ct);
}
