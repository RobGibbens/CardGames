using System;
using CardGames.Poker.Api.Infrastructure.Caching;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Infrastructure.Caching;

public sealed class GameCacheKeysTests
{
	[Fact]
	public void GameTag_FormatsCorrectly()
	{
		var gameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		GameCacheKeys.GameTag(gameId).Should().Be("game:11111111-1111-1111-1111-111111111111");
	}

	[Fact]
	public void GamePlayersTag_FormatsCorrectly()
	{
		var gameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
		GameCacheKeys.GamePlayersTag(gameId).Should().Be("game-players:22222222-2222-2222-2222-222222222222");
	}

	[Fact]
	public void BettingRoundTag_FormatsCorrectly()
	{
		var gameId = Guid.Parse("33333333-3333-3333-3333-333333333333");
		GameCacheKeys.BettingRoundTag(gameId).Should().Be("betting-round:33333333-3333-3333-3333-333333333333");
	}

	[Fact]
	public void DrawPlayerTag_FormatsCorrectly()
	{
		var gameId = Guid.Parse("44444444-4444-4444-4444-444444444444");
		GameCacheKeys.DrawPlayerTag(gameId).Should().Be("draw-player:44444444-4444-4444-4444-444444444444");
	}

	[Fact]
	public void CurrentPlayerTurnTag_FormatsCorrectly()
	{
		var gameId = Guid.Parse("55555555-5555-5555-5555-555555555555");
		GameCacheKeys.CurrentPlayerTurnTag(gameId).Should().Be("current-player-turn:55555555-5555-5555-5555-555555555555");
	}

	[Fact]
	public void BuildPerGameTags_ReturnsAllPerGameTags()
	{
		var gameId = Guid.NewGuid();

		var tags = GameCacheKeys.BuildPerGameTags(gameId);

		tags.Should().HaveCount(5);
		tags.Should().Contain(GameCacheKeys.GameTag(gameId));
		tags.Should().Contain(GameCacheKeys.GamePlayersTag(gameId));
		tags.Should().Contain(GameCacheKeys.BettingRoundTag(gameId));
		tags.Should().Contain(GameCacheKeys.DrawPlayerTag(gameId));
		tags.Should().Contain(GameCacheKeys.CurrentPlayerTurnTag(gameId));
	}

	[Fact]
	public void BuildAllMutationTags_IncludesPerGameAndActiveGamesTags()
	{
		var gameId = Guid.NewGuid();

		var tags = GameCacheKeys.BuildAllMutationTags(gameId);

		tags.Should().HaveCount(6);
		tags.Should().Contain(GameCacheKeys.ActiveGamesTag);
		tags.Should().Contain(GameCacheKeys.GameTag(gameId));
	}

	[Fact]
	public void ActiveGamesTag_HasExpectedValue()
	{
		GameCacheKeys.ActiveGamesTag.Should().Be("active-games");
	}

	[Fact]
	public void AvailableGamesTag_HasExpectedValue()
	{
		GameCacheKeys.AvailableGamesTag.Should().Be("available-games");
	}
}
