#nullable enable

using System.Linq;
using System.Security.Claims;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class GameUserIdResolverTests
{
    private readonly GameUserIdResolver _sut = new();

    // ── ResolveFromClaims ───────────────────────────────────────────────

    [Fact]
    public void ResolveFromClaims_NullPrincipal_ReturnsNull()
    {
        _sut.ResolveFromClaims(null).Should().BeNull();
    }

    [Fact]
    public void ResolveFromClaims_PrefersClaimTypesEmail()
    {
        var principal = CreatePrincipal(
            (ClaimTypes.Email, "alice@example.com"),
            ("email", "other@example.com"),
            ("preferred_username", "alice"),
            (ClaimTypes.Name, "Alice"));

        _sut.ResolveFromClaims(principal).Should().Be("alice@example.com");
    }

    [Fact]
    public void ResolveFromClaims_FallsBackToOidcEmail()
    {
        var principal = CreatePrincipal(
            ("email", "bob@example.com"),
            ("preferred_username", "bob"),
            (ClaimTypes.Name, "Bob"));

        _sut.ResolveFromClaims(principal).Should().Be("bob@example.com");
    }

    [Fact]
    public void ResolveFromClaims_FallsBackToPreferredUsername()
    {
        var principal = CreatePrincipal(
            ("preferred_username", "carol"),
            (ClaimTypes.Name, "Carol"));

        _sut.ResolveFromClaims(principal).Should().Be("carol");
    }

    [Fact]
    public void ResolveFromClaims_FallsBackToIdentityName()
    {
        var principal = CreatePrincipal((ClaimTypes.Name, "Dave"));

        _sut.ResolveFromClaims(principal).Should().Be("Dave");
    }

    [Fact]
    public void ResolveFromClaims_NoClaims_ReturnsNull()
    {
        var principal = CreatePrincipal();

        _sut.ResolveFromClaims(principal).Should().BeNull();
    }

    [Fact]
    public void ResolveFromClaims_DoesNotFallBackToNameIdentifier()
    {
        // NameIdentifier is typically an internal GUID, not suitable for player-record matching.
        var principal = CreatePrincipal((ClaimTypes.NameIdentifier, "some-guid-value"));

        // Identity.Name is null when ClaimTypes.Name is absent, so result should be null.
        _sut.ResolveFromClaims(principal).Should().BeNull();
    }

    // ── ResolveFromPlayer ───────────────────────────────────────────────

    [Fact]
    public void ResolveFromPlayer_PrefersEmail()
    {
        _sut.ResolveFromPlayer("alice@example.com", "Alice", "ext-123")
            .Should().Be("alice@example.com");
    }

    [Fact]
    public void ResolveFromPlayer_MalformedEmail_PrefersName()
    {
        _sut.ResolveFromPlayer("alice@example.com@localhost", "alice@example.com", "ext-123")
            .Should().Be("alice@example.com");
    }

    [Fact]
    public void ResolveFromPlayer_MalformedEmail_NoName_FallsBackToEmail()
    {
        _sut.ResolveFromPlayer("alice@example.com@localhost", null, "ext-123")
            .Should().Be("alice@example.com@localhost");
    }

    [Fact]
    public void ResolveFromPlayer_MalformedEmail_WhitespaceName_FallsBackToEmail()
    {
        _sut.ResolveFromPlayer("bad@email@localhost", "  ", "ext-123")
            .Should().Be("bad@email@localhost");
    }

    [Fact]
    public void ResolveFromPlayer_NullEmail_FallsBackToName()
    {
        _sut.ResolveFromPlayer(null, "Bob", "ext-456")
            .Should().Be("Bob");
    }

    [Fact]
    public void ResolveFromPlayer_NullEmailAndName_FallsBackToExternalId()
    {
        _sut.ResolveFromPlayer(null, null, "ext-789")
            .Should().Be("ext-789");
    }

    [Fact]
    public void ResolveFromPlayer_AllNull_ReturnsNull()
    {
        _sut.ResolveFromPlayer(null, null, null)
            .Should().BeNull();
    }

    [Fact]
    public void ResolveFromPlayer_SingleAtEmail_UsesEmail()
    {
        _sut.ResolveFromPlayer("valid@example.com", "SomeName", null)
            .Should().Be("valid@example.com");
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = claims.Length > 0
            ? new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Test")
            : new ClaimsIdentity();
        return new ClaimsPrincipal(identity);
    }
}
