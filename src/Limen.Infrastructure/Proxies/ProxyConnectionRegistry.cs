using System.Collections.Concurrent;
using Limen.Application.Common.Interfaces;

namespace Limen.Infrastructure.Proxies;

public sealed class ProxyConnectionRegistry : IProxyConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, IAgentChannel> _channels = new();

    public Task RegisterAsync(Guid proxyNodeId, IAgentChannel channel)
    {
        _channels.AddOrUpdate(proxyNodeId, channel, (key, old) =>
        {
            _ = old.CloseAsync();
            return channel;
        });
        return Task.CompletedTask;
    }

    public void Unregister(Guid proxyNodeId) => _channels.TryRemove(proxyNodeId, out _);
    public IAgentChannel? Get(Guid proxyNodeId) => _channels.TryGetValue(proxyNodeId, out var c) ? c : null;
    public IReadOnlyCollection<Guid> ListOnlineProxyNodeIds() => _channels.Keys.ToArray();
}
