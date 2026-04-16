using System.Collections.Concurrent;
using System.Security.Cryptography;
using Limen.Application.Common.Interfaces;

namespace Limen.Infrastructure.Auth;

/// <summary>
/// In-memory OIDC state store for resource-level OIDC flows.
/// Single-instance only: multi-replica deployments need a DB-backed replacement (v1 tech debt).
/// </summary>
public sealed class ResourceOidcStateStore : IResourceOidcStateStore
{
    private const int MaxEntries = 10_000;

    private sealed record Entry(Guid RouteId, string ReturnTo, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public string CreateState(Guid routeId, string returnTo)
    {
        PruneExpired();
        if (_entries.Count >= MaxEntries)
        {
            throw new InvalidOperationException("Resource OIDC state store is at capacity.");
        }

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
        PruneExpired();
        return (entry.RouteId, entry.ReturnTo);
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _entries)
        {
            if (kv.Value.ExpiresAt < now) { _entries.TryRemove(kv.Key, out _); }
        }
    }
}
