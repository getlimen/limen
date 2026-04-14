# Plan 03 — WireGuard: `forculus` hub + tunneled agent traffic

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`.

**Goal:** Scaffold the `forculus` repo. Forculus runs `wireguard-go`/`wg` and exposes a small HTTP API for peer management. Limen pushes peer updates on change and Forculus pulls full config on boot + every 60s as reconcile. Limentinus brings up its own WG interface (userspace wireguard-go embedded) to tunnel toward Forculus. All subsequent control traffic flows over the tunnel.

**Architecture:** Forculus = stateless hub; Postgres (via Limen) is source of truth. HTTP REST for peer management. Subprocess `wg` CLI via `wg syncconf` for applying config. Limentinus embeds `wireguard-go` userspace binary and talks to it via UAPI unix socket.

**Prerequisites:** Plan 2 complete. Limentinus enrolls and heartbeats over plain WS. Now we wrap that in WG.

---

## File structure

**In `limen` repo:**
- `contracts/Limen.Contracts/ForculusHttp/{PeerSpec.cs, ConfigSnapshot.cs}`
- `src/Limen.Domain/Tunnels/WireGuardPeer.cs`
- `src/Limen.Application/Services/TunnelCoordinator.cs`
- `src/Limen.Application/Commands/Tunnels/{AllocateTunnelAddressCommand.cs, RotateForculusKeysCommand.cs}`
- `src/Limen.Infrastructure/Tunnels/ForculusHttpClient.cs`
- Modify: `EnrollAgentCommand` to allocate WG keypair + tunnel IP at enrollment time
- Modify: `EnrollResponse` to include WG config for agent

**In `forculus` repo (new):**
- Full clean-arch scaffold
- `src/Forculus.Application/Commands/Peers/{ApplyFullConfigCommand.cs, UpsertPeerCommand.cs, RemovePeerCommand.cs}`
- `src/Forculus.Application/Services/PeerReconciler.cs`
- `src/Forculus.Infrastructure/WireGuard/{WgCliDriver.cs, ConfigWriter.cs}`
- `src/Forculus.Infrastructure/Control/LimenHttpClient.cs`
- `src/Forculus.API/` with HTTP endpoints: `POST /peers`, `DELETE /peers/{pubkey}`, `GET /config`, `GET /stats`
- `compose.yml` + `Dockerfile`

**In `limentinus` repo:**
- `src/Limentinus.Infrastructure/Tunnel/WireGuardGoClient.cs`
- `src/Limentinus.Infrastructure/Tunnel/UapiSocketClient.cs`
- Embedded `wireguard-go` binary
- Modify `Program.cs` to bring up WG before WS reconnect

---

## Tasks

### Task 1: Forculus HTTP contracts

Add to `limen/contracts/Limen.Contracts/ForculusHttp/`:

```csharp
// PeerSpec.cs
namespace Limen.Contracts.ForculusHttp;

public sealed record PeerSpec(string PublicKey, string AllowedIps, string? PresharedKey = null);

// ConfigSnapshot.cs
public sealed record ConfigSnapshot(
    string ServerPrivateKey,
    int ListenPort,
    string InterfaceAddress,       // "10.42.0.1/24"
    IReadOnlyList<PeerSpec> Peers);
```

Build + commit.

### Task 2: WireGuardPeer domain entity + migration

```csharp
// src/Limen.Domain/Tunnels/WireGuardPeer.cs
namespace Limen.Domain.Tunnels;

public class WireGuardPeer
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public string TunnelIp { get; set; } = string.Empty;      // "10.42.0.17/32"
    public string? PrivateKeyForAgent { get; set; }           // provided once at enroll, then null
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
```

EF config, add to DbContext, generate migration `Tunnels`.

### Task 3: TunnelCoordinator + tunnel IP allocator

```csharp
// src/Limen.Application/Services/TunnelCoordinator.cs
using Limen.Application.Common.Interfaces;
using Limen.Domain.Tunnels;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Services;

public sealed class TunnelCoordinator
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public TunnelCoordinator(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    // Naive allocator: find first free /32 in 10.42.0.0/24 starting at .2
    public async Task<string> AllocateTunnelIpAsync(CancellationToken ct)
    {
        var used = await _db.WireGuardPeers
            .Where(p => p.RevokedAt == null)
            .Select(p => p.TunnelIp)
            .ToListAsync(ct);
        var usedSet = used.Select(ip => int.Parse(ip.Split('.')[3].Split('/')[0])).ToHashSet();
        for (int i = 2; i <= 250; i++)
            if (!usedSet.Contains(i)) return $"10.42.0.{i}/32";
        throw new InvalidOperationException("Subnet exhausted");
    }

    public (string pub, string priv) GenerateKeypair()
    {
        // Use Curve25519 via BouncyCastle or NSec.Cryptography
        // (Stub shown — real impl must match WireGuard's key format)
        throw new NotImplementedException();
    }
}
```

**Keypair generation:** use `NSec.Cryptography` (MIT, AOT-safe):

```csharp
using NSec.Cryptography;
// ...
public (string pub, string priv) GenerateKeypair()
{
    using var key = Key.Create(KeyAgreementAlgorithm.X25519,
        new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
    var privBytes = key.Export(KeyBlobFormat.RawPrivateKey);
    var pubBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
    return (Convert.ToBase64String(pubBytes), Convert.ToBase64String(privBytes));
}
```

Add `<PackageReference Include="NSec.Cryptography" Version="25.4.0" />` to Limen.Application.

### Task 4: Modify EnrollAgentCommand to include WG provisioning

Update `EnrollAgentCommandHandler` in Limen:

```csharp
// After creating agent, create a WireGuardPeer:
var (pub, priv) = _tunnels.GenerateKeypair();
var tunnelIp = await _tunnels.AllocateTunnelIpAsync(ct);
var peer = new WireGuardPeer
{
    Id = Guid.NewGuid(),
    AgentId = agent.Id,
    PublicKey = pub,
    TunnelIp = tunnelIp,
    PrivateKeyForAgent = priv,     // will be included ONCE in response, then nulled
    CreatedAt = now,
};
_db.WireGuardPeers.Add(peer);
await _db.SaveChangesAsync(ct);

// push to Forculus via HTTP
await _forculus.UpsertPeerAsync(new PeerSpec(pub, tunnelIp), ct);
```

Update `EnrollResponse` in contracts to include `WireGuardConfig`:

```csharp
public sealed record WireGuardConfig(
    string InterfaceAddress,
    string PrivateKey,
    string ServerPublicKey,
    string ServerEndpoint,     // "203.0.113.42:51820"
    int KeepaliveSeconds);

public sealed record EnrollResponse(
    Guid AgentId,
    string PermanentSecret,
    WireGuardConfig Wireguard);
```

### Task 5: `ForculusHttpClient` in Limen.Infrastructure

```csharp
// src/Limen.Infrastructure/Tunnels/ForculusHttpClient.cs
using System.Net.Http.Json;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.ForculusHttp;

namespace Limen.Infrastructure.Tunnels;

public sealed class ForculusHttpClient : IForculusClient
{
    private readonly HttpClient _http;
    public ForculusHttpClient(HttpClient http) => _http = http;

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

    public async Task<ConfigSnapshot> GetConfigAsync(CancellationToken ct)
        => (await _http.GetFromJsonAsync<ConfigSnapshot>("/config", ct))!;
}
```

Register as typed `HttpClient`:
```csharp
services.AddHttpClient<IForculusClient, ForculusHttpClient>(c =>
    c.BaseAddress = new Uri(config["Forculus:BaseUrl"] ?? "http://forculus:3004"));
```

### Task 6: Scaffold `forculus` repo

```bash
cd C:/GithubProjects/getlimen/forculus
git init
dotnet new slnx -n Forculus
dotnet new classlib -n Forculus.Domain -o src/Forculus.Domain
dotnet new classlib -n Forculus.Application -o src/Forculus.Application
dotnet new classlib -n Forculus.Infrastructure -o src/Forculus.Infrastructure
dotnet new web -n Forculus.API -o src/Forculus.API
dotnet new xunit -n Forculus.Tests -o src/Forculus.Tests
# add to slnx, set NativeAOT on Forculus.API
```

Copy Directory.Build.props, .editorconfig, .gitignore, LICENSE from the boilerplate.

### Task 7: Forculus — WgCliDriver

```csharp
// src/Forculus.Infrastructure/WireGuard/WgCliDriver.cs
using System.Diagnostics;
using System.Text;
using Forculus.Application.Common.Interfaces;
using Limen.Contracts.ForculusHttp;
using Microsoft.Extensions.Logging;

namespace Forculus.Infrastructure.WireGuard;

public sealed class WgCliDriver : IWireGuardDriver
{
    private readonly ILogger<WgCliDriver> _log;
    private readonly string _iface;
    public WgCliDriver(ILogger<WgCliDriver> log, string iface = "wg0") { _log = log; _iface = iface; }

    public async Task ApplyConfigAsync(ConfigSnapshot snapshot, CancellationToken ct)
    {
        var configPath = "/etc/wireguard/wg0.conf";
        var content = RenderConfig(snapshot);
        await File.WriteAllTextAsync(configPath, content, ct);
        await RunAsync("wg-quick", $"strip {configPath} > /tmp/wg0.stripped", ct);
        await RunAsync("wg", $"syncconf {_iface} /tmp/wg0.stripped", ct);
    }

    public async Task UpsertPeerAsync(PeerSpec peer, CancellationToken ct)
    {
        await RunAsync("wg", $"set {_iface} peer {peer.PublicKey} allowed-ips {peer.AllowedIps}", ct);
    }

    public async Task RemovePeerAsync(string publicKey, CancellationToken ct)
    {
        await RunAsync("wg", $"set {_iface} peer {publicKey} remove", ct);
    }

    private string RenderConfig(ConfigSnapshot s)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"Address = {s.InterfaceAddress}");
        sb.AppendLine($"ListenPort = {s.ListenPort}");
        sb.AppendLine($"PrivateKey = {s.ServerPrivateKey}");
        foreach (var p in s.Peers)
        {
            sb.AppendLine();
            sb.AppendLine("[Peer]");
            sb.AppendLine($"PublicKey = {p.PublicKey}");
            sb.AppendLine($"AllowedIPs = {p.AllowedIps}");
            if (p.PresharedKey is not null) sb.AppendLine($"PresharedKey = {p.PresharedKey}");
        }
        return sb.ToString();
    }

    private async Task RunAsync(string cmd, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(cmd, args) { RedirectStandardError = true, RedirectStandardOutput = true };
        using var p = Process.Start(psi)!;
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
        {
            var err = await p.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"`{cmd} {args}` failed: {err}");
        }
    }
}
```

Interface:
```csharp
// src/Forculus.Application/Common/Interfaces/IWireGuardDriver.cs
using Limen.Contracts.ForculusHttp;
namespace Forculus.Application.Common.Interfaces;
public interface IWireGuardDriver
{
    Task ApplyConfigAsync(ConfigSnapshot s, CancellationToken ct);
    Task UpsertPeerAsync(PeerSpec p, CancellationToken ct);
    Task RemovePeerAsync(string publicKey, CancellationToken ct);
}
```

### Task 8: Forculus — LimenHttpClient + PeerReconciler

```csharp
// src/Forculus.Infrastructure/Control/LimenHttpClient.cs
using Forculus.Application.Common.Interfaces;
using Limen.Contracts.ForculusHttp;
using System.Net.Http.Json;

namespace Forculus.Infrastructure.Control;

public sealed class LimenHttpClient : ILimenClient
{
    private readonly HttpClient _http;
    public LimenHttpClient(HttpClient http) => _http = http;

    public async Task<ConfigSnapshot> FetchConfigAsync(CancellationToken ct)
        => (await _http.GetFromJsonAsync<ConfigSnapshot>("/api/forculus/config", ct))!;
}
```

```csharp
// src/Forculus.Application/Services/PeerReconciler.cs
using Forculus.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Forculus.Application.Services;

public sealed class PeerReconciler : BackgroundService
{
    private readonly ILimenClient _limen;
    private readonly IWireGuardDriver _wg;
    private readonly ILogger<PeerReconciler> _log;

    public PeerReconciler(ILimenClient limen, IWireGuardDriver wg, ILogger<PeerReconciler> log)
    { _limen = limen; _wg = wg; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Boot: pull full config with retry
        var snapshot = await RetryAsync(() => _limen.FetchConfigAsync(ct), ct);
        await _wg.ApplyConfigAsync(snapshot, ct);
        _log.LogInformation("WG applied with {N} peers", snapshot.Peers.Count);

        // Reconcile every 60s
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
            try
            {
                var latest = await _limen.FetchConfigAsync(ct);
                await _wg.ApplyConfigAsync(latest, ct);
            }
            catch (Exception ex) { _log.LogWarning(ex, "Reconcile failed; will retry"); }
        }
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> op, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(5);
        while (!ct.IsCancellationRequested)
        {
            try { return await op(); }
            catch (Exception ex) { _log.LogWarning(ex, "Retry in {Delay}", delay); await Task.Delay(delay, ct); delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2)); }
        }
        throw new OperationCanceledException();
    }
}
```

### Task 9: Forculus HTTP endpoints

```csharp
// src/Forculus.API/Program.cs
using Forculus.Application.Commands.Peers;
using Forculus.Application.Common.Interfaces;
using Forculus.Application.Services;
using Forculus.Infrastructure.Control;
using Forculus.Infrastructure.WireGuard;
using Limen.Contracts.ForculusHttp;
using Mediator;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((_, cfg) => cfg.WriteTo.Console());
builder.Services.AddMediator(opt => opt.ServiceLifetime = ServiceLifetime.Scoped);

builder.Services.AddSingleton<IWireGuardDriver>(sp =>
    new WgCliDriver(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WgCliDriver>>()));
builder.Services.AddHttpClient<ILimenClient, LimenHttpClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Limen:BaseUrl"] ?? "http://limen:8080"));
builder.Services.AddHostedService<PeerReconciler>();

var app = builder.Build();
app.MapPost("/peers", async (PeerSpec p, IMediator m, CancellationToken ct) =>
    { await m.Send(new UpsertPeerCommand(p), ct); return Results.Ok(); });
app.MapDelete("/peers/{pubkey}", async (string pubkey, IMediator m, CancellationToken ct) =>
    { await m.Send(new RemovePeerCommand(pubkey), ct); return Results.Ok(); });
app.MapGet("/config", async (ILimenClient limen, CancellationToken ct) =>
    Results.Ok(await limen.FetchConfigAsync(ct)));

app.Run();
```

Implement `UpsertPeerCommand` and `RemovePeerCommand` in Application (each calling `IWireGuardDriver`).

### Task 10: Forculus Dockerfile + compose

```dockerfile
# forculus/src/Forculus.API/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Forculus.API/Forculus.API.csproj -c Release -r linux-x64 -o /app \
    /p:PublishAot=true

FROM alpine:3.19
RUN apk add --no-cache wireguard-tools iproute2 iptables
WORKDIR /app
COPY --from=build /app .
EXPOSE 3004 51820/udp
ENTRYPOINT ["./Forculus.API"]
```

Add `forculus` service to `limen/compose.yml`:
```yaml
  forculus:
    image: ghcr.io/getlimen/forculus:latest
    restart: unless-stopped
    cap_add: [NET_ADMIN]
    sysctls:
      - net.ipv4.ip_forward=1
    volumes:
      - forculus_state:/etc/wireguard
    ports:
      - "51820:51820/udp"
    environment:
      Limen__BaseUrl: http://limen:8080
```

### Task 11: Limentinus WG integration

Add `wireguard-go` binary to the Limentinus Docker image. Create `WireGuardGoClient` that:
- Launches `wireguard-go wg0` subprocess
- Uses the UAPI unix socket at `/var/run/wireguard/wg0.sock`
- Applies the `WireGuardConfig` received from the EnrollResponse

```csharp
// src/Limentinus.Infrastructure/Tunnel/WireGuardGoClient.cs
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Limen.Contracts.AgentMessages;
using Limentinus.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Limentinus.Infrastructure.Tunnel;

public sealed class WireGuardGoClient : IWireGuardClient
{
    private readonly ILogger<WireGuardGoClient> _log;
    private Process? _daemon;

    public WireGuardGoClient(ILogger<WireGuardGoClient> log) => _log = log;

    public async Task BringUpAsync(WireGuardConfig cfg, CancellationToken ct)
    {
        // Launch wireguard-go
        _daemon = Process.Start(new ProcessStartInfo("/usr/local/bin/wireguard-go", "-f wg0")
        {
            RedirectStandardError = true, RedirectStandardOutput = true,
        });
        await Task.Delay(500, ct);  // wait for socket

        // Set config via UAPI
        var uapi = $"set=1\n" +
                   $"private_key={HexFromBase64(cfg.PrivateKey)}\n" +
                   $"replace_peers=true\n" +
                   $"public_key={HexFromBase64(cfg.ServerPublicKey)}\n" +
                   $"endpoint={cfg.ServerEndpoint}\n" +
                   $"persistent_keepalive_interval={cfg.KeepaliveSeconds}\n" +
                   $"allowed_ip=0.0.0.0/0\n\n";
        using var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await s.ConnectAsync(new UnixDomainSocketEndPoint("/var/run/wireguard/wg0.sock"), ct);
        await s.SendAsync(Encoding.UTF8.GetBytes(uapi), ct);
        var buf = new byte[4096];
        var n = await s.ReceiveAsync(buf, ct);
        _log.LogInformation("UAPI response: {Resp}", Encoding.UTF8.GetString(buf, 0, n));

        // Assign interface IP via `ip` command
        Process.Start("ip", $"address add {cfg.InterfaceAddress} dev wg0")?.WaitForExit();
        Process.Start("ip", "link set up dev wg0")?.WaitForExit();
    }

    public Task TearDownAsync() { _daemon?.Kill(); return Task.CompletedTask; }

    private static string HexFromBase64(string b64) => Convert.ToHexString(Convert.FromBase64String(b64)).ToLowerInvariant();
}
```

Modify `Program.cs` to call `BringUpAsync(enrollResponse.Wireguard)` after enrollment and before starting the persistent WS loop.

### Task 12: End-to-end smoke test

1. Start Limen + Postgres + Forculus via `compose.yml`
2. Create provisioning key in UI
3. Run Limentinus (with WG CAP_NET_ADMIN) in a second container
4. Verify: `docker exec <limentinus> wg show wg0` shows a peer with handshake
5. Verify: ping central's WG IP from Limentinus (`ping 10.42.0.1`)
6. Verify: WS reconnect now goes through the tunnel (inspect tcpdump)

Commit + push.

---

## Exit criteria for Plan 3

✅ `forculus` repo scaffolded, AOT-compiled
✅ `wg syncconf` applies config from Limen
✅ 60s reconcile loop in Forculus
✅ Limentinus embeds wireguard-go, tunnel up after enrollment
✅ All agent↔Limen traffic now over WG tunnel
✅ Tunnel IPs allocated in 10.42.0.0/24

**Plan 4 unlocks next:** Ostiarius reverse proxy with Let's Encrypt.
