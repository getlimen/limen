namespace Limen.Application.Common.Interfaces;

public interface IProxyConnectionRegistry
{
    Task RegisterAsync(Guid proxyNodeId, IAgentChannel channel);
    void Unregister(Guid proxyNodeId);
    IAgentChannel? Get(Guid proxyNodeId);
    IReadOnlyCollection<Guid> ListOnlineProxyNodeIds();
}
