using System.Collections.Concurrent;
using Limen.Application.Common.Interfaces;

namespace Limen.Infrastructure.Agents;

public sealed class AgentConnectionRegistry : IAgentConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, IAgentChannel> _channels = new();

    public async Task RegisterAsync(Guid agentId, IAgentChannel channel)
    {
        if (_channels.TryGetValue(agentId, out var old))
        {
            await old.CloseAsync();
        }
        _channels[agentId] = channel;
    }

    public void Unregister(Guid agentId) => _channels.TryRemove(agentId, out _);
    public IAgentChannel? Get(Guid agentId) => _channels.TryGetValue(agentId, out var c) ? c : null;
    public IReadOnlyCollection<Guid> ListOnlineAgentIds() => _channels.Keys.ToArray();
}
