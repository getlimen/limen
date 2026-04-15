namespace Limen.Application.Common.Interfaces;

public interface IAgentConnectionRegistry
{
    Task RegisterAsync(Guid agentId, IAgentChannel channel);
    void Unregister(Guid agentId);
    IAgentChannel? Get(Guid agentId);
    IReadOnlyCollection<Guid> ListOnlineAgentIds();
}

public interface IAgentChannel
{
    Task SendJsonAsync<T>(string type, T payload, CancellationToken ct);
    Task CloseAsync();
}
