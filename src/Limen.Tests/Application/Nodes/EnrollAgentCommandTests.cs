using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Limen.Application.Commands.Nodes;
using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using Limen.Contracts.AgentMessages;
using Limen.Domain.Nodes;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Limen.Tests.Application.Nodes;

public sealed class EnrollAgentCommandTests
{
    private static (AppDbContext db, IClock clock, ITunnelCoordinator tunnels, IForculusClient forculus, IOptions<WgServerSettings> wgOpts, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 14, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        var tunnels = Substitute.For<ITunnelCoordinator>();
        tunnels.AllocateTunnelIpAsync(Arg.Any<CancellationToken>()).Returns("10.42.0.2/32");
        tunnels.GenerateKeypair().Returns(("pubkey_base64==", "privkey_base64=="));

        var forculus = Substitute.For<IForculusClient>();

        var wgOpts = Options.Create(new WgServerSettings
        {
            PublicKey = "server_pubkey_base64==",
            Endpoint = "203.0.113.1:51820",
        });

        return (db, clock, tunnels, forculus, wgOpts, now);
    }

    private static EnrollAgentCommandHandler MakeHandler(AppDbContext db, IClock clock, ITunnelCoordinator tunnels, IForculusClient forculus, IOptions<WgServerSettings> wgOpts)
        => new(db, clock, tunnels, forculus, wgOpts, NullLogger<EnrollAgentCommandHandler>.Instance);

    private static (string plaintext, ProvisioningKey row) SeedKey(AppDbContext db, DateTimeOffset now, TimeSpan? ttl = null, DateTimeOffset? usedAt = null)
    {
        var plaintext = "test-plaintext-key-with-enough-entropy-abcdef";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
        var pk = new ProvisioningKey
        {
            Id = Guid.NewGuid(),
            KeyHash = hash,
            IntendedRoles = new[] { "docker" },
            CreatedAt = now.AddMinutes(-1),
            ExpiresAt = now + (ttl ?? TimeSpan.FromMinutes(15)),
            UsedAt = usedAt,
        };
        db.ProvisioningKeys.Add(pk);
        db.SaveChanges();
        return (plaintext, pk);
    }

    [Fact]
    public async Task Creates_node_agent_wireguard_peer_and_burns_key()
    {
        var (db, clock, tunnels, forculus, wgOpts, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now);
        var handler = MakeHandler(db, clock, tunnels, forculus, wgOpts);

        var result = await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        result.AgentId.Should().NotBe(Guid.Empty);
        result.Secret.Should().NotBeNullOrWhiteSpace();
        result.Wireguard.Should().NotBeNull();
        result.Wireguard.InterfaceAddress.Should().Be("10.42.0.2/32");
        result.Wireguard.PrivateKey.Should().Be("privkey_base64==");
        result.Wireguard.ServerPublicKey.Should().Be("server_pubkey_base64==");
        result.Wireguard.ServerEndpoint.Should().Be("203.0.113.1:51820");
        result.Wireguard.KeepaliveSeconds.Should().Be(25);

        (await db.Nodes.CountAsync()).Should().Be(1);
        (await db.Agents.CountAsync()).Should().Be(1);
        (await db.WireGuardPeers.CountAsync()).Should().Be(1);

        var peer = await db.WireGuardPeers.FirstAsync();
        peer.AgentId.Should().Be(result.AgentId);
        peer.PublicKey.Should().Be("pubkey_base64==");
        peer.TunnelIp.Should().Be("10.42.0.2/32");
        peer.RevokedAt.Should().BeNull();

        var key = await db.ProvisioningKeys.FirstAsync();
        key.UsedAt.Should().Be(now);
        key.ResultingNodeId.Should().NotBeNull();

        await tunnels.Received(1).AllocateTunnelIpAsync(Arg.Any<CancellationToken>());
        tunnels.Received(1).GenerateKeypair();
        await forculus.Received(1).UpsertPeerAsync(
            Arg.Is<Limen.Contracts.ForculusHttp.PeerSpec>(p => p.PublicKey == "pubkey_base64==" && p.AllowedIps == "10.42.0.2/32"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Forculus_failure_is_swallowed_and_enrollment_succeeds()
    {
        var (db, clock, tunnels, forculus, wgOpts, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now);
        forculus.UpsertPeerAsync(Arg.Any<Limen.Contracts.ForculusHttp.PeerSpec>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("forculus unavailable"));
        var handler = MakeHandler(db, clock, tunnels, forculus, wgOpts);

        var result = await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        result.AgentId.Should().NotBe(Guid.Empty);
        (await db.WireGuardPeers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Throws_on_expired_key()
    {
        var (db, clock, tunnels, forculus, wgOpts, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now, ttl: TimeSpan.FromMinutes(-1));
        var handler = MakeHandler(db, clock, tunnels, forculus, wgOpts);

        var act = async () => await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task Throws_on_already_used_key()
    {
        var (db, clock, tunnels, forculus, wgOpts, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now, usedAt: now.AddSeconds(-30));
        var handler = MakeHandler(db, clock, tunnels, forculus, wgOpts);

        var act = async () => await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*used*");
    }

    [Fact]
    public async Task Throws_on_unknown_key()
    {
        var (db, clock, tunnels, forculus, wgOpts, _) = MakeContext();
        var handler = MakeHandler(db, clock, tunnels, forculus, wgOpts);

        var act = async () => await handler.Handle(
            new EnrollAgentCommand("random-nonexistent-key", "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Invalid*");
    }
}
