using System;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class PlayerJoinedToastPolicyTests
{
	[Fact]
	public void IsOwnJoin_SamePlayerId_IsTheViewersJoin()
	{
		var playerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		var notification = Notification(playerId, "Alice");

		PlayerJoinedToastPolicy.IsOwnJoin(notification, playerId, "Bob", "bob@example.com")
			.Should().BeTrue();
	}

	[Fact]
	public void IsOwnJoin_SameName_IsTheViewersJoin()
	{
		var notification = Notification(Guid.NewGuid(), "Alice");

		PlayerJoinedToastPolicy.IsOwnJoin(notification, Guid.NewGuid(), "alice", "bob@example.com")
			.Should().BeTrue();
	}

	[Fact]
	public void IsOwnJoin_SameEmail_IsTheViewersJoin()
	{
		var notification = Notification(Guid.NewGuid(), "Alice@Example.com");

		PlayerJoinedToastPolicy.IsOwnJoin(notification, Guid.NewGuid(), "Bob", "alice@example.com")
			.Should().BeTrue();
	}

	[Fact]
	public void IsOwnJoin_DifferentIdNameAndEmail_IsNotTheViewersJoin()
	{
		var notification = Notification(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alice");

		PlayerJoinedToastPolicy.IsOwnJoin(
				notification,
				Guid.Parse("22222222-2222-2222-2222-222222222222"),
				"Bob",
				"bob@example.com")
			.Should().BeFalse();
	}

	private static PlayerJoinedDto Notification(Guid playerId, string playerName) =>
		new()
		{
			GameId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
			PlayerId = playerId,
			PlayerName = playerName,
			SeatIndex = 1,
			CanPlayCurrentHand = true,
			Message = $"{playerName} has joined the table!"
		};
}
