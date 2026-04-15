using System.Net.Http.Json;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.ForculusHttp;

namespace Limen.Infrastructure.Tunnels;

public sealed class ForculusHttpClient : IForculusClient
{
    private readonly HttpClient _http;
    public ForculusHttpClient(HttpClient http) { _http = http; }

    public async Task UpsertPeerAsync(PeerSpec peer, CancellationToken ct)
    {
        var res = await _http.PostAsJsonAsync("/peers", peer, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task RemovePeerAsync(string publicKey, CancellationToken ct)
    {
        var res = await _http.DeleteAsync($"/peers/{Uri.EscapeDataString(publicKey)}", ct);
        res.EnsureSuccessStatusCode();
    }
}
