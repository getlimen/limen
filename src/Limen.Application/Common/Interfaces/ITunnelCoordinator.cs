namespace Limen.Application.Common.Interfaces;

public interface ITunnelCoordinator
{
    Task<string> AllocateTunnelIpAsync(CancellationToken ct);
    (string publicKeyBase64, string privateKeyBase64) GenerateKeypair();
}
