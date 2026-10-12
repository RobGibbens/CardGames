using System;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class GameConnectionRegistryTests
{
	[Fact]
	public void GetConnectionIds_TwoConnectionsForOnePlayer_ReturnsBoth()
	{
		var registry = new GameConnectionRegistry();
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();

		registry.Add(gameId, playerId, "c1");
		registry.Add(gameId, playerId, "c2");

		registry.GetConnectionIds(gameId, playerId).Should().BeEquivalentTo("c1", "c2");
	}

	[Fact]
	public void Remove_OneOfTwoConnections_LeavesTheOther()
	{
		var registry = new GameConnectionRegistry();
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();
		registry.Add(gameId, playerId, "c1");
		registry.Add(gameId, playerId, "c2");

		registry.Remove("c1");

		registry.GetConnectionIds(gameId, playerId).Should().Equal("c2");
	}

	[Fact]
	public void Add_ConnectionMovesToAnotherPlayer_LeavesTheFirstPlayer()
	{
		var registry = new GameConnectionRegistry();
		var gameId = Guid.NewGuid();
		var firstPlayerId = Guid.NewGuid();
		var secondPlayerId = Guid.NewGuid();
		registry.Add(gameId, firstPlayerId, "c1");
		registry.Add(gameId, firstPlayerId, "c2");

		registry.Add(gameId, secondPlayerId, "c1");

		registry.GetConnectionIds(gameId, firstPlayerId).Should().Equal("c2");
		registry.GetConnectionIds(gameId, secondPlayerId).Should().Equal("c1");
	}

	[Fact]
	public void GetConnectionIds_UnknownPair_ReturnsEmptyList()
	{
		var registry = new GameConnectionRegistry();

		var connectionIds = registry.GetConnectionIds(Guid.NewGuid(), Guid.NewGuid());

		connectionIds.Should().NotBeNull().And.BeEmpty();
	}
}
