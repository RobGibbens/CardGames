using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.HoldEm;
using CardGames.Poker.Games.KingsAndLows;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class AutoActionSeatResolverTests
{
	[Fact]
	public async Task ResolveAsync_PreFlop_UsesIncompleteBettingRoundActor()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, "PreFlop", currentPlayerIndex: 1, currentDrawPlayerIndex: -1);
		context.BettingRounds.Add(new BettingRound
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 1,
			Street = "PreFlop",
			CurrentActorIndex = 2,
			IsComplete = false,
			StartedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();

		var target = await AutoActionSeatResolver.ResolveAsync(
			context, game, HoldEmRules.CreateGameRules(), CancellationToken.None);

		target.Kind.Should().Be(AutoActionTargetKind.Betting);
		target.SeatIndex.Should().Be(2);
		target.BettingRoundId.Should().NotBeNull();
		target.UsedCurrentPlayerIndexFallback.Should().BeFalse();
	}

	[Fact]
	public async Task ResolveAsync_BettingWithoutRound_FallsBackToCurrentPlayerIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, "FirstBettingRound", currentPlayerIndex: 3, currentDrawPlayerIndex: -1);

		var target = await AutoActionSeatResolver.ResolveAsync(
			context, game, FiveCardDrawRules.CreateGameRules(), CancellationToken.None);

		target.Kind.Should().Be(AutoActionTargetKind.Betting);
		target.SeatIndex.Should().Be(3);
		target.BettingRoundId.Should().BeNull();
		target.UsedCurrentPlayerIndexFallback.Should().BeTrue();
	}

	[Fact]
	public async Task ResolveAsync_DrawPhase_UsesCurrentDrawPlayerIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, "DrawPhase", currentPlayerIndex: 0, currentDrawPlayerIndex: 4);

		var target = await AutoActionSeatResolver.ResolveAsync(
			context, game, FiveCardDrawRules.CreateGameRules(), CancellationToken.None);

		target.Kind.Should().Be(AutoActionTargetKind.Draw);
		target.SeatIndex.Should().Be(4);
	}

	[Fact]
	public async Task ResolveAsync_DropOrStay_IsSimultaneous()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, "DropOrStay", currentPlayerIndex: 1, currentDrawPlayerIndex: -1);

		var target = await AutoActionSeatResolver.ResolveAsync(
			context, game, KingsAndLowsRules.CreateGameRules(), CancellationToken.None);

		target.Kind.Should().Be(AutoActionTargetKind.Simultaneous);
		target.SeatIndex.Should().Be(-1);
	}

	[Fact]
	public async Task ResolveAsync_Showdown_RequiresNoActor()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, "Showdown", currentPlayerIndex: 1, currentDrawPlayerIndex: -1);

		var target = await AutoActionSeatResolver.ResolveAsync(
			context, game, HoldEmRules.CreateGameRules(), CancellationToken.None);

		target.Kind.Should().Be(AutoActionTargetKind.None);
		target.SeatIndex.Should().Be(-1);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string phase,
		int currentPlayerIndex,
		int currentDrawPlayerIndex)
	{
		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = phase,
			CurrentHandNumber = 1,
			CurrentPlayerIndex = currentPlayerIndex,
			CurrentDrawPlayerIndex = currentDrawPlayerIndex,
			GameType = new GameType { Code = "HOLDEM", Name = "Texas Hold'em" }
		};

		context.Games.Add(game);
		await context.SaveChangesAsync();
		return game;
	}
}
