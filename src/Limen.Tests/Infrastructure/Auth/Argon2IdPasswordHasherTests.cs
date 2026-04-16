using FluentAssertions;
using Limen.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace Limen.Tests.Infrastructure.Auth;

public sealed class Argon2IdPasswordHasherTests
{
    private readonly Argon2IdPasswordHasher _hasher = new(NullLogger<Argon2IdPasswordHasher>.Instance);

    [Fact]
    public void Hash_and_verify_round_trip()
    {
        var hash = _hasher.Hash("my-password");
        _hasher.Verify("my-password", hash).Should().BeTrue();
    }

    [Fact]
    public void Wrong_password_returns_false()
    {
        var hash = _hasher.Hash("correct-password");
        _hasher.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_starts_with_phc_prefix()
    {
        var hash = _hasher.Hash("test");
        hash.Should().StartWith("$argon2id$");
    }

    [Fact]
    public void Two_hashes_of_same_password_are_different_due_to_salt()
    {
        var hash1 = _hasher.Hash("same-password");
        var hash2 = _hasher.Hash("same-password");
        hash1.Should().NotBe(hash2);
    }
}
