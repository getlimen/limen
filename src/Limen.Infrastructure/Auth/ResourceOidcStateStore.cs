using System.Collections.Concurrent;
using System.Security.Cryptography;
using Limen.Application.Common.Interfaces;

namespace Limen.Infrastructure.Auth;

public sealed class ResourceOidcStateStore : IResourceOidcStateStore
{
    private sealed record Entry(Guid RouteId, string ReturnTo, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public string CreateState(Guid routeId, string returnTo)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
        _entries[token] = new Entry(routeId, returnTo, expiry);
        return token;
    }

    public (Guid RouteId, string ReturnTo)? ConsumeState(string state)
    {
        if (!_entries.TryRemove(state, out var entry)) { return null; }
        if (entry.ExpiresAt < DateTimeOffset.UtcNow) { return null; }
        // Prune stale entries opportunistically
        foreach (var key in _entries.Keys)
        {
            if (_entries.TryGetValue(key, out var e) && e.ExpiresAt < DateTimeOffset.UtcNow)
            {
                _entries.TryRemove(key, out _);
            }
        }
        return (entry.RouteId, entry.ReturnTo);
    }
}
