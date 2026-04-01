using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Infrastructure.Caching;

public sealed class GameStateQueryCacheInvalidatorTests
{
	private readonly HybridCache _hybridCache = Substitute.For<HybridCache>();
	private readonly ILogger<GameStateQueryCacheInvalidator> _logger =
		Substitute.For<ILogger<GameStateQueryCacheInvalidator>>();

	private GameStateQueryCacheInvalidator CreateSut() => new(_hybridCache, _logger);

	[Fact]
	public async Task InvalidateAfterMutationAsync_RemovesAllMutationTags()
	{
		var gameId = Guid.NewGuid();
		var sut = CreateSut();

		await sut.InvalidateAfterMutationAsync(gameId);

		var expectedTags = GameCacheKeys.BuildAllMutationTags(gameId);
		foreach (var tag in expectedTags)
		{
			await _hybridCache.Received(1).RemoveByTagAsync(tag, Arg.Any<CancellationToken>());
		}
	}

	[Fact]
	public async Task InvalidateGameAsync_RemovesOnlyPerGameTags()
	{
		var gameId = Guid.NewGuid();
		var sut = CreateSut();

		await sut.InvalidateGameAsync(gameId);

		var expectedTags = GameCacheKeys.BuildPerGameTags(gameId);
		foreach (var tag in expectedTags)
		{
			await _hybridCache.Received(1).RemoveByTagAsync(tag, Arg.Any<CancellationToken>());
		}

		// Should NOT remove active-games tag
		await _hybridCache.DidNotReceive().RemoveByTagAsync(GameCacheKeys.ActiveGamesTag, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task InvalidateActiveGamesAsync_RemovesOnlyActiveGamesTag()
	{
		var sut = CreateSut();

		await sut.InvalidateActiveGamesAsync();

		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.ActiveGamesTag, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task InvalidateAfterMutationAsync_RemovesPerGameAndActiveGamesTags()
	{
		var gameId = Guid.NewGuid();
		var sut = CreateSut();

		await sut.InvalidateAfterMutationAsync(gameId);

		// Verify per-game tags
		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.GameTag(gameId), Arg.Any<CancellationToken>());
		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.GamePlayersTag(gameId), Arg.Any<CancellationToken>());
		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.BettingRoundTag(gameId), Arg.Any<CancellationToken>());
		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.DrawPlayerTag(gameId), Arg.Any<CancellationToken>());
		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.CurrentPlayerTurnTag(gameId), Arg.Any<CancellationToken>());

		// Verify global tag
		await _hybridCache.Received(1).RemoveByTagAsync(GameCacheKeys.ActiveGamesTag, Arg.Any<CancellationToken>());
	}
}
