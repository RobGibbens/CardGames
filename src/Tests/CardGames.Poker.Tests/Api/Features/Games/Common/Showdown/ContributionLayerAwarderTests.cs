using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using CardGames.Poker.Betting;
using FluentAssertions;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Common.Showdown;

public class ContributionLayerAwarderTests
{
	[Fact]
	public async Task Award_WhenAFoldedPlayerMatchedPartOfTheMainPot_LeavesThoseChipsInThePot()
	{
		await using var context = CreateContext();
		var game = await SeedAsync(context, 290,
		[
			new Seat("Alice", 0, 110, false, false, 30),
			new Seat("Bob", 1, 60, true, false, 20),
			new Seat("Carol", 2, 110, false, false, 10),
			new Seat("Doug", 3, 10, false, true, 0)
		]);

		var result = await AwardAsync(context, game, ("Alice", 20L), ("Bob", 30L), ("Carol", 10L));

		result.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 190, ["Alice"] = 100 });
		var pots = await context.Pots.Include(pot => pot.Contributions).ThenInclude(contribution => contribution.GamePlayer)
			.ThenInclude(gamePlayer => gamePlayer.Player).ToListAsync();
		var main = pots.Single(pot => pot.PotOrder == 0);
		main.Amount.Should().Be(190);
		main.Contributions.Single(contribution => contribution.GamePlayer.Player.Name == "Doug").IsEligibleToWin
			.Should().BeFalse();
		(await PlayerAsync(context, "Doug")).ChipStack.Should().Be(0);
		(await PlayerAsync(context, "Doug")).TotalContributedThisHand.Should().Be(10);
	}

	[Fact]
	public async Task Award_WhenAnAllInHappenedOnAnEarlierStreet_UsesTheCumulativeTotal()
	{
		await using var context = CreateContext();
		var game = await SeedAsync(context, 900,
		[
			new Seat("Alice", 0, 100, true, false, 30),
			new Seat("Bob", 1, 400, false, false, 10),
			new Seat("Carol", 2, 400, false, false, 20)
		]);
		foreach (var gamePlayer in game.GamePlayers)
			gamePlayer.CurrentBet = 0;

		var result = await AwardAsync(context, game, ("Alice", 30L), ("Bob", 10L), ("Carol", 20L));

		result.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 300, ["Carol"] = 600 });
	}

	[Fact]
	public async Task Award_WhenTwoPlayersAreAllInForLess_PaysEachLayerToItsBestHand()
	{
		await using var context = CreateContext();
		var game = await SeedAsync(context, 270,
		[
			new Seat("A", 0, 20, true, false, 40),
			new Seat("B", 1, 50, true, false, 30),
			new Seat("C", 2, 100, false, false, 20),
			new Seat("D", 3, 100, false, false, 10)
		]);

		var result = await AwardAsync(context, game, ("A", 40L), ("B", 30L), ("C", 20L), ("D", 10L));

		result.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["A"] = 80, ["B"] = 90, ["C"] = 100 });
		result.Payouts.Should().NotContainKey("D");
	}

	[Fact]
	public async Task Award_WhenContributionsAreEqual_PaysTheWholePotToTheBestHand()
	{
		await using var context = CreateContext();
		var game = await SeedAsync(context, 120,
		[
			new Seat("Alice", 0, 40, false, false, 30),
			new Seat("Bob", 1, 40, false, false, 20),
			new Seat("Carol", 2, 40, false, false, 10)
		]);

		var result = await AwardAsync(context, game, ("Alice", 30L), ("Bob", 20L), ("Carol", 10L));

		result.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 120 });
		(await PlayerAsync(context, "Alice")).TotalContributedThisHand.Should().Be(40);
		(await context.Pots.ToListAsync()).Should().ContainSingle().Which.Amount.Should().Be(120);
	}

	[Fact]
	public async Task Award_WhenABuyOrTollIsInsideTheContribution_KeepsItInTheContestedPot()
	{
		await using var context = CreateContext();
		var matched = await SeedAsync(context, 200,
		[
			new Seat("Alice", 0, 100, true, false, 20),
			new Seat("Bob", 1, 100, false, false, 10)
		]);

		var matchedResult = await AwardAsync(context, matched, ("Alice", 20L), ("Bob", 10L));

		matchedResult.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 200 });
		(await PlayerAsync(context, "Alice")).TotalContributedThisHand.Should().Be(100);

		await using var largerContext = CreateContext();
		var larger = await SeedAsync(largerContext, 400,
		[
			new Seat("Alice", 0, 100, true, false, 20),
			new Seat("Carol", 1, 300, false, false, 10)
		]);

		var largerResult = await AwardAsync(largerContext, larger, ("Alice", 20L), ("Carol", 10L));

		largerResult.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 200 });
		var carol = await PlayerAsync(largerContext, "Carol");
		carol.ChipStack.Should().Be(200);
		carol.TotalContributedThisHand.Should().Be(100);
		largerResult.Payouts.Should().NotContainKey("Carol");
	}

	[Fact]
	public async Task Award_WhenThePotDoesNotDivideEvenly_GivesTheOddChipClockwiseFromTheDealer()
	{
		await using var context = CreateContext();
		var game = await SeedAsync(context, 101,
		[
			new Seat("Alice", 0, 50, false, false, 10),
			new Seat("Bob", 2, 50, false, false, 10),
			new Seat("Doug", 3, 1, false, true, 0)
		]);
		game.DealerPosition = 1;

		var result = await AwardAsync(context, game, ("Alice", 10L), ("Bob", 10L));

		result.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 51, ["Alice"] = 50 });
	}

	[Fact]
	public async Task Award_WhenOnlyEliminatedPlayersCoverASidePot_RefundsThatLayer()
	{
		await using var context = CreateContext();
		var game = await SeedAsync(context, 700,
		[
			new Seat("Alice", 0, 100, true, false, 10),
			new Seat("Bob", 1, 300, true, false, 99),
			new Seat("Carol", 2, 300, true, false, 5)
		]);
		foreach (var name in new[] { "Bob", "Carol" })
			(await PlayerAsync(context, name)).VariantState = "UGLY_ELIMINATED";

		var result = await AwardAsync(
			context,
			game,
			player => player.VariantState != "UGLY_ELIMINATED",
			("Alice", 10L),
			("Bob", 99L),
			("Carol", 5L));

		result.Payouts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Alice"] = 300 });
		(await PlayerAsync(context, "Bob")).ChipStack.Should().Be(200);
		(await PlayerAsync(context, "Bob")).TotalContributedThisHand.Should().Be(100);
		(await PlayerAsync(context, "Carol")).ChipStack.Should().Be(200);
		(await PlayerAsync(context, "Carol")).TotalContributedThisHand.Should().Be(100);
		var pots = await context.Pots.ToListAsync();
		pots.Should().ContainSingle().Which.Amount.Should().Be(300);
	}

	private static async Task<ContributionLayerAwardResult> AwardAsync(
		CardsDbContext context,
		Game game,
		params (string Name, long Strength)[] strengths)
	{
		return await AwardAsync(context, game, _ => true, strengths);
	}

	private static async Task<ContributionLayerAwardResult> AwardAsync(
		CardsDbContext context,
		Game game,
		Func<GamePlayer, bool> canWin,
		params (string Name, long Strength)[] strengths)
	{
		var byId = game.GamePlayers
			.Where(gamePlayer => strengths.Any(strength => strength.Name == gamePlayer.Player.Name))
			.ToDictionary(
				gamePlayer => gamePlayer.Id,
				gamePlayer => strengths.Single(strength => strength.Name == gamePlayer.Player.Name).Strength);
		var awarded = await new ContributionLayerAwarder(context).AwardAsync(
			game,
			game.Pots.Where(pot => pot.HandNumber == game.CurrentHandNumber).ToList(),
			DateTimeOffset.UtcNow,
			false,
			byId,
			canWin,
			gamePlayerId => "Pair",
			CancellationToken.None);
		awarded.IsT0.Should().BeTrue(awarded.IsT1 ? awarded.AsT1 : string.Empty);
		await context.SaveChangesAsync();
		return awarded.AsT0;
	}

	private static async Task<GamePlayer> PlayerAsync(CardsDbContext context, string name)
	{
		return await context.GamePlayers.Include(gamePlayer => gamePlayer.Player)
			.SingleAsync(gamePlayer => gamePlayer.Player.Name == name);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;
		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedAsync(CardsDbContext context, int potAmount, IReadOnlyList<Seat> seats)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "SEVENCARDSTUD",
			Name = "Seven Card Stud",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 2,
			InitialBoardCards = 1,
			MaxCommunityCards = 0,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
			CreatedAt = now,
			UpdatedAt = now
		};
		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = nameof(Phases.Showdown),
			CurrentHandNumber = 1,
			DealerPosition = 0,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		context.Add(gameType);
		context.Add(game);
		foreach (var seat in seats)
		{
			var player = new Player
			{
				Id = Guid.NewGuid(),
				Name = seat.Name,
				CreatedAt = now,
				UpdatedAt = now
			};
			var gamePlayer = new GamePlayer
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				PlayerId = player.Id,
				Player = player,
				SeatPosition = seat.SeatNumber,
				ChipStack = 0,
				TotalContributedThisHand = seat.Contributed,
				IsAllIn = seat.AllIn,
				HasFolded = seat.Folded,
				Status = GamePlayerStatus.Active,
				JoinedAt = now
			};
			game.GamePlayers.Add(gamePlayer);
			context.Add(player);
			context.Add(gamePlayer);
		}

		var pot = new PotEntity
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			CreatedAt = now
		};
		game.Pots.Add(pot);
		context.Pots.Add(pot);
		await context.SaveChangesAsync();
		return game;
	}

	private sealed record Seat(string Name, int SeatNumber, int Contributed, bool AllIn, bool Folded, long Strength);
}
