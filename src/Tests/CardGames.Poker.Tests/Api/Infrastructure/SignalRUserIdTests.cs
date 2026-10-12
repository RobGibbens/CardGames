#nullable enable

using System.Security.Claims;
using CardGames.Poker.Api.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Infrastructure;

public class SignalRUserIdTests
{
	[Fact]
	public void ForConnection_EmailAndNameIdentifier_ReturnsEmailIncludingTwoAtSigns()
	{
		var user = Principal(
			new Claim(ClaimTypes.Email, "alice@friends.local@localhost"),
			new Claim(ClaimTypes.NameIdentifier, "alice-subject"));

		SignalRUserId.ForConnection(user).Should().Be("alice@friends.local@localhost");
	}

	[Fact]
	public void ForConnection_SubOnly_ReturnsSub()
	{
		var user = Principal(new Claim("sub", "alice-subject"));

		SignalRUserId.ForConnection(user).Should().Be("alice-subject");
	}

	[Fact]
	public void ForConnection_PreferredUsernameAndSubWithoutEmail_ReturnsSub()
	{
		var user = Principal(
			new Claim("preferred_username", "alice@friends.local"),
			new Claim("sub", "alice-subject"));

		SignalRUserId.ForConnection(user).Should().Be("alice-subject");
	}

	[Fact]
	public void RoutingKey_PrefersSignalRUserIdThenEmailThenExternalIdThenName()
	{
		SignalRUserId.RoutingKey(
				"alice-subject",
				"alice@friends.local@localhost",
				"ext-alice",
				"alice@friends.local")
			.Should().Be("alice-subject");

		SignalRUserId.RoutingKey(null, "alice@friends.local@localhost", "ext-alice", "alice@friends.local")
			.Should().Be("alice@friends.local@localhost");

		SignalRUserId.RoutingKey(null, null, "ext-alice", "alice@friends.local")
			.Should().Be("ext-alice");

		SignalRUserId.RoutingKey(null, null, null, "alice@friends.local")
			.Should().Be("alice@friends.local");
	}

	[Fact]
	public void RoutingKey_DoesNotReturnNameWhenEmailIsSet()
	{
		SignalRUserId.RoutingKey(null, "alice@friends.local@localhost", null, "alice@friends.local")
			.Should().Be("alice@friends.local@localhost");
	}

	[Fact]
	public void SelectPlayer_WhenBobsNameEqualsAlicesEmail_ReturnsAlice()
	{
		var aliceEmail = "alice@friends.local@localhost";
		var bob = new Seat("bob", null, "bob@friends.local", null, aliceEmail);
		var alice = new Seat("alice", null, aliceEmail, null, "alice@friends.local");

		var selected = SignalRUserId.SelectPlayer(
			[bob, alice],
			aliceEmail,
			seat => seat.SignalRUserId,
			seat => seat.Email,
			seat => seat.ExternalId,
			seat => seat.Name);

		selected.Should().BeSameAs(alice);
	}

	[Fact]
	public void SelectPlayer_StoredSignalRUserId_WinsOverEmail()
	{
		var alice = new Seat(
			"alice",
			"alice-subject",
			"alice@friends.local@localhost",
			null,
			"alice@friends.local");

		var selected = SignalRUserId.SelectPlayer(
			[alice],
			"alice-subject",
			seat => seat.SignalRUserId,
			seat => seat.Email,
			seat => seat.ExternalId,
			seat => seat.Name);

		selected.Should().BeSameAs(alice);
	}

	[Fact]
	public void SelectPlayer_NameIsNotUsedWhenEmailIsSet()
	{
		var alice = new Seat("alice", null, "alice@friends.local@localhost", null, "alice@friends.local");

		var selected = SignalRUserId.SelectPlayer(
			[alice],
			"alice@friends.local",
			seat => seat.SignalRUserId,
			seat => seat.Email,
			seat => seat.ExternalId,
			seat => seat.Name);

		selected.Should().BeNull();
	}

	private static ClaimsPrincipal Principal(params Claim[] claims) =>
		new(new ClaimsIdentity(claims, "test"));

	private sealed record Seat(
		string Id,
		string? SignalRUserId,
		string? Email,
		string? ExternalId,
		string? Name);
}
