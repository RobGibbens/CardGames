using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.Common.Showdown;

/// <summary>
///     Showdown awards. Hole cards and the board are fixed. Expected payouts are written
///     in the cases; hand strength comes from <see cref="HoldemHand" />, not from a copied pot formula.
/// </summary>
public class SidePotAwardScenarioTests
{
	private const string QuietBoard = "2c 3d 4h 5s 9c";
	private const string RoyalBoard = "Ah Kh Qh Jh Th";

	public static TheoryData<string> CaseIds()
	{
		var data = new TheoryData<string>();
		foreach (var caseId in Cases.Keys)
			data.Add(caseId);
		return data;
	}

	[Theory]
	[MemberData(nameof(CaseIds))]
	public async Task Award_ExplicitShowdown_PaysEachPotToTheEligibleHand(string caseId)
	{
		var scenario = Cases[caseId];
		await using var context = CreateContext();
		var game = await SeedAsync(context, scenario);

		var strengths = Strengths(scenario);
		if (scenario.FoldedHoleBeatsTheField is not null)
		{
			var folded = StrengthOf(scenario.FoldedHoleBeatsTheField, scenario.Board);
			strengths.Values.Should().NotBeEmpty();
			folded.Should().BeGreaterThan(strengths.Values.Max());
		}

		var awarded = await AwardAsync(context, game, strengths);
		awarded.Payouts.Should().BeEquivalentTo(scenario.Payouts);

		var pots = await context.Pots.Include(pot => pot.Contributions)
			.ThenInclude(contribution => contribution.GamePlayer)
			.ThenInclude(gamePlayer => gamePlayer.Player)
			.OrderBy(pot => pot.PotOrder)
			.ToListAsync();
		pots.Select(pot => pot.Amount).Should().Equal(scenario.Pots.Select(pot => pot.Amount));
		pots.Should().OnlyContain(pot => pot.IsAwarded && pot.Amount > 0);

		for (var index = 0; index < scenario.Pots.Length; index++)
		{
			var pot = pots[index];
			var expected = scenario.Pots[index];
			pot.Contributions.Where(contribution => contribution.IsEligibleToWin)
				.Select(contribution => contribution.GamePlayer.Player.Name)
				.Should().BeEquivalentTo(expected.Eligible);
			pot.Contributions.Where(contribution => !contribution.IsEligibleToWin)
				.Select(contribution => contribution.GamePlayer.Player.Name)
				.Should().BeEquivalentTo(expected.Ineligible);
			Winners(pot).Should().BeEquivalentTo(expected.Winners);
			Winners(pot).Select(winner => winner.Name).Should().OnlyHaveUniqueItems();
			Winners(pot).Sum(winner => winner.Amount).Should().Be(pot.Amount);
		}

		foreach (var seat in scenario.Seats)
		{
			var player = await context.GamePlayers.Include(gamePlayer => gamePlayer.Player)
				.SingleAsync(gamePlayer => gamePlayer.Player.Name == seat.Name);
			player.ChipStack.Should().Be(scenario.FinalStacks[seat.Name]);
			player.ChipStack.Should().BeGreaterThanOrEqualTo(0);
			if (seat.Folded)
				awarded.Payouts.Should().NotContainKey(seat.Name);
		}

		scenario.FinalStacks.Values.Sum().Should().Be(scenario.Seats.Sum(seat => seat.StartingStack));
		var refunds = scenario.Seats.Sum(seat =>
			scenario.FinalStacks[seat.Name] - (seat.StartingStack - seat.Contribution) -
			awarded.Payouts.GetValueOrDefault(seat.Name));
		(awarded.Payouts.Values.Sum() + refunds).Should().Be(scenario.Seats.Sum(seat => seat.Contribution));
	}

	[Fact]
	public async Task Award_WhenSettlementRunsAgain_DoesNotCreditChipsTwice()
	{
		var scenario = Cases["12-short-wins-main-other-wins-side"];
		await using var context = CreateContext();
		var game = await SeedAsync(context, scenario);
		var first = await AwardAsync(context, game, Strengths(scenario));
		var stacks = await StacksAsync(context);

		var second = await new ContributionLayerAwarder(context).AwardAsync(
			game,
			first.Pots,
			DateTimeOffset.UtcNow,
			true,
			Strengths(scenario),
			_ => true,
			_ => "Pair",
			CancellationToken.None);
		second.IsT0.Should().BeTrue();
		second.AsT0.Payouts.Should().BeEquivalentTo(first.Payouts);
		(await StacksAsync(context)).Should().BeEquivalentTo(stacks);
	}

	[Fact]
	public async Task Award_WhenThePotTotalDoesNotMatchContributions_ReturnsAnError()
	{
		await using var context = CreateContext();
		var scenario = Cases["01-two-players-one-winner"];
		var game = await SeedAsync(context, scenario);
		game.Pots.Single().Amount += 1;

		var awarded = await new ContributionLayerAwarder(context).AwardAsync(
			game,
			game.Pots.ToList(),
			DateTimeOffset.UtcNow,
			false,
			Strengths(scenario),
			_ => true,
			_ => "Pair",
			CancellationToken.None);

		awarded.IsT1.Should().BeTrue();
		awarded.AsT1.Should().Contain("does not equal contributed chips");
	}

	[Fact]
	public async Task Award_WhenNobodyIsLeftToContest_RefusesToDropThePot()
	{
		await using var context = CreateContext();
		var scenario = Cases["39-last-player"];
		var game = await SeedAsync(context, scenario);
		foreach (var gamePlayer in game.GamePlayers)
			gamePlayer.HasFolded = true;

		var awarded = await new ContributionLayerAwarder(context).AwardAsync(
			game,
			game.Pots.ToList(),
			DateTimeOffset.UtcNow,
			false,
			new Dictionary<Guid, long>(),
			_ => true,
			_ => "Pair",
			CancellationToken.None);

		awarded.IsT1.Should().BeTrue();
		awarded.AsT1.Should().Contain("do not equal contributed chips");
		(await StacksAsync(context)).Values.Sum().Should().Be(scenario.Seats.Sum(seat => seat.StartingStack - seat.Contribution));
	}

	[Fact]
	public void Kicker_FullHandStrengthSeparatesTwoAceHighHands()
	{
		// 5s would make both hands the wheel, so the king and queen would never play.
		var board = "2c 3d 4h 8s 9c".ToCards();
		var king = new HoldemHand("Ah Kd".ToCards(), board);
		var queen = new HoldemHand("As Qd".ToCards(), board);

		king.Type.Should().Be(HandType.HighCard);
		queen.Type.Should().Be(king.Type);
		king.Strength.Should().BeGreaterThan(queen.Strength);
	}

	[Fact]
	public void Board_BothPlayersPlayTheSameFiveCards()
	{
		var board = RoyalBoard.ToCards();
		var left = new HoldemHand("2c 3d".ToCards(), board);
		var right = new HoldemHand("4c 5d".ToCards(), board);

		left.Strength.Should().Be(right.Strength);
	}

	private static Dictionary<Guid, long> Strengths(ShowdownCase scenario)
	{
		return scenario.Seats
			.Where(seat => !seat.Folded)
			.ToDictionary(seat => seat.Id, seat => StrengthOf(seat.Hole, scenario.Board));
	}

	private static long StrengthOf(string hole, string board) =>
		new HoldemHand(hole.ToCards(), board.ToCards()).Strength;

	private static async Task<ContributionLayerAwardResult> AwardAsync(
		CardsDbContext context,
		Game game,
		IReadOnlyDictionary<Guid, long> strengths)
	{
		var awarded = await new ContributionLayerAwarder(context).AwardAsync(
			game,
			game.Pots.Where(pot => pot.HandNumber == game.CurrentHandNumber).ToList(),
			DateTimeOffset.UtcNow,
			false,
			strengths,
			_ => true,
			_ => "Pair",
			CancellationToken.None);
		awarded.IsT0.Should().BeTrue(awarded.IsT1 ? awarded.AsT1 : string.Empty);
		await context.SaveChangesAsync();
		return awarded.AsT0;
	}

	private static IReadOnlyList<(string Name, int Amount)> Winners(PotEntity pot)
	{
		var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(pot.WinnerPayouts!);
		return rows!.Select(row => (row["playerName"].GetString()!, row["amount"].GetInt32())).ToList();
	}

	private static async Task<Dictionary<string, int>> StacksAsync(CardsDbContext context)
	{
		return await context.GamePlayers.Include(gamePlayer => gamePlayer.Player)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot())
			.Options;
		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedAsync(CardsDbContext context, ShowdownCase scenario)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "HOLDEM",
			Name = "Texas Hold'em",
			BettingStructure = BettingStructure.Blinds,
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 2,
			InitialBoardCards = 0,
			MaxCommunityCards = 5,
			MaxPlayerCards = 2,
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
			DealerPosition = scenario.DealerSeat,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};
		context.Add(gameType);
		context.Add(game);
		foreach (var seat in scenario.Seats)
		{
			var player = new Player
			{
				Id = Guid.NewGuid(),
				Name = seat.Name,
				CreatedAt = now,
				UpdatedAt = now
			};
			var remaining = seat.StartingStack - seat.Contribution;
			var gamePlayer = new GamePlayer
			{
				Id = seat.Id,
				GameId = game.Id,
				Game = game,
				PlayerId = player.Id,
				Player = player,
				SeatPosition = seat.Seat,
				ChipStack = remaining,
				TotalContributedThisHand = seat.Contribution,
				IsAllIn = seat.AllIn || remaining == 0,
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
			Amount = scenario.Seats.Sum(seat => seat.Contribution),
			CreatedAt = now
		};
		game.Pots.Add(pot);
		context.Pots.Add(pot);
		await context.SaveChangesAsync();
		return game;
	}

	private static Dictionary<string, ShowdownCase> Cases { get; } = new()
	{
		["01-two-players-one-winner"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 200, 100, "Ah Ad"),
			S("Bob", 1, 200, 100, "Kd Qd"),
			pots: [Pot(200, ["Ann", "Bob"], [], ("Ann", 200))],
			payouts: new() { ["Ann"] = 200 },
			stacks: new() { ["Ann"] = 300, ["Bob"] = 100 }),
		["02-three-equal-one-winner"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 150, 40, "Ah Ad"),
			S("Bob", 1, 150, 40, "Kd Qd"),
			S("Cal", 2, 150, 40, "7c 2d"),
			pots: [Pot(120, ["Ann", "Bob", "Cal"], [], ("Ann", 120))],
			payouts: new() { ["Ann"] = 120 },
			stacks: new() { ["Ann"] = 230, ["Bob"] = 110, ["Cal"] = 110 }),
		["04-two-players-tie"] = Show(
			RoyalBoard, 0,
			S("Ann", 1, 100, 50, "2c 3d"),
			S("Bob", 2, 100, 50, "4c 5d"),
			pots: [Pot(100, ["Ann", "Bob"], [], ("Ann", 50), ("Bob", 50))],
			payouts: new() { ["Ann"] = 50, ["Bob"] = 50 },
			stacks: new() { ["Ann"] = 100, ["Bob"] = 100 }),
		["12-short-wins-main-other-wins-side"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 100, 100, "Ah Ad", allIn: true),
			S("Bob", 1, 400, 200, "Kh Kd"),
			S("Cal", 2, 400, 200, "Qh Qd"),
			pots:
			[
				Pot(300, ["Ann", "Bob", "Cal"], [], ("Ann", 300)),
				Pot(200, ["Bob", "Cal"], [], ("Bob", 200))
			],
			payouts: new() { ["Ann"] = 300, ["Bob"] = 200 },
			stacks: new() { ["Ann"] = 300, ["Bob"] = 400, ["Cal"] = 200 }),
		["12-cal-wins-the-side"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 100, 100, "Ah Ad", allIn: true),
			S("Bob", 1, 400, 200, "Qh Qd"),
			S("Cal", 2, 400, 200, "Kh Kd"),
			pots:
			[
				Pot(300, ["Ann", "Bob", "Cal"], [], ("Ann", 300)),
				Pot(200, ["Bob", "Cal"], [], ("Cal", 200))
			],
			payouts: new() { ["Ann"] = 300, ["Cal"] = 200 },
			stacks: new() { ["Ann"] = 300, ["Bob"] = 200, ["Cal"] = 400 }),
		["44-main-outright-side-splits"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 40, 40, "Ah Ad", allIn: true),
			S("Bob", 1, 200, 100, "Kh Kd"),
			S("Cal", 2, 200, 100, "Ks Kc"),
			pots:
			[
				Pot(120, ["Ann", "Bob", "Cal"], [], ("Ann", 120)),
				Pot(120, ["Bob", "Cal"], [], ("Bob", 60), ("Cal", 60))
			],
			payouts: new() { ["Ann"] = 120, ["Bob"] = 60, ["Cal"] = 60 },
			stacks: new() { ["Ann"] = 120, ["Bob"] = 160, ["Cal"] = 160 }),
		["13-short-loses-deeper-wins-both"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 100, 100, "7c 2d", allIn: true),
			S("Bob", 1, 400, 200, "Ah Ad"),
			S("Cal", 2, 400, 200, "Qh Qd"),
			pots:
			[
				Pot(300, ["Ann", "Bob", "Cal"], [], ("Bob", 300)),
				Pot(200, ["Bob", "Cal"], [], ("Bob", 200))
			],
			payouts: new() { ["Bob"] = 500 },
			stacks: new() { ["Ann"] = 0, ["Bob"] = 700, ["Cal"] = 200 }),
		["14-short-ties-main-other-wins-side"] = Show(
			QuietBoard, 0,
			S("Ann", 1, 50, 50, "Ah Ad", allIn: true),
			S("Bob", 2, 50, 50, "As Ac", allIn: true),
			S("Cal", 3, 400, 200, "Kh Kd"),
			S("Dee", 4, 400, 200, "Qh Qd"),
			pots:
			[
				Pot(200, ["Ann", "Bob", "Cal", "Dee"], [], ("Ann", 100), ("Bob", 100)),
				Pot(300, ["Cal", "Dee"], [], ("Cal", 300))
			],
			payouts: new() { ["Ann"] = 100, ["Bob"] = 100, ["Cal"] = 300 },
			stacks: new() { ["Ann"] = 100, ["Bob"] = 100, ["Cal"] = 500, ["Dee"] = 200 }),
		["15-mid-stack-wins-main-and-first-side-only"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 50, 50, "Jh Jd", allIn: true),
			S("Bob", 1, 100, 100, "Ah Ad", allIn: true),
			S("Cal", 2, 400, 200, "Kh Kd"),
			S("Dee", 3, 400, 200, "Qh Qd"),
			pots:
			[
				Pot(200, ["Ann", "Bob", "Cal", "Dee"], [], ("Bob", 200)),
				Pot(150, ["Bob", "Cal", "Dee"], [], ("Bob", 150)),
				Pot(200, ["Cal", "Dee"], [], ("Cal", 200))
			],
			payouts: new() { ["Bob"] = 350, ["Cal"] = 200 },
			stacks: new() { ["Ann"] = 0, ["Bob"] = 350, ["Cal"] = 400, ["Dee"] = 200 }),
		["21-largest-loses-every-pot"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 50, 50, "Ah Ad", allIn: true),
			S("Bob", 1, 100, 100, "Kh Kd", allIn: true),
			S("Cal", 2, 400, 200, "Qh Qd"),
			S("Dee", 3, 400, 200, "7c 2d"),
			pots:
			[
				Pot(200, ["Ann", "Bob", "Cal", "Dee"], [], ("Ann", 200)),
				Pot(150, ["Bob", "Cal", "Dee"], [], ("Bob", 150)),
				Pot(200, ["Cal", "Dee"], [], ("Cal", 200))
			],
			payouts: new() { ["Ann"] = 200, ["Bob"] = 150, ["Cal"] = 200 },
			stacks: new() { ["Ann"] = 200, ["Bob"] = 150, ["Cal"] = 400, ["Dee"] = 200 }),
		["22-three-different-winners"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 50, 50, "Ah Ad", allIn: true),
			S("Bob", 1, 100, 100, "Kh Kd", allIn: true),
			S("Cal", 2, 400, 200, "Qh Qd"),
			S("Dee", 3, 400, 200, "Jh Jd"),
			pots:
			[
				Pot(200, ["Ann", "Bob", "Cal", "Dee"], [], ("Ann", 200)),
				Pot(150, ["Bob", "Cal", "Dee"], [], ("Bob", 150)),
				Pot(200, ["Cal", "Dee"], [], ("Cal", 200))
			],
			payouts: new() { ["Ann"] = 200, ["Bob"] = 150, ["Cal"] = 200 },
			stacks: new() { ["Ann"] = 200, ["Bob"] = 150, ["Cal"] = 400, ["Dee"] = 200 }),
		["23-one-player-wins-both-side-pots"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 10, 10, "Ah Ad", allIn: true),
			S("Cal", 1, 40, 40, "Qh Qd", allIn: true),
			S("Bob", 2, 100, 100, "Kh Kd", allIn: true),
			S("Dee", 3, 200, 100, "Jh Jd"),
			pots:
			[
				Pot(40, ["Ann", "Cal", "Bob", "Dee"], [], ("Ann", 40)),
				Pot(90, ["Cal", "Bob", "Dee"], [], ("Bob", 90)),
				Pot(120, ["Bob", "Dee"], [], ("Bob", 120))
			],
			payouts: new() { ["Ann"] = 40, ["Bob"] = 210 },
			stacks: new() { ["Ann"] = 40, ["Cal"] = 0, ["Bob"] = 210, ["Dee"] = 100 }),
		["26-best-hand-wins-every-pot"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 50, 50, "7c 2d", allIn: true),
			S("Cal", 1, 100, 100, "3c 3d", allIn: true),
			S("Bob", 2, 400, 200, "Ah Ad"),
			S("Dee", 3, 400, 200, "4c 4d"),
			pots:
			[
				Pot(200, ["Ann", "Cal", "Bob", "Dee"], [], ("Bob", 200)),
				Pot(150, ["Cal", "Bob", "Dee"], [], ("Bob", 150)),
				Pot(200, ["Bob", "Dee"], [], ("Bob", 200))
			],
			payouts: new() { ["Bob"] = 550 },
			stacks: new() { ["Ann"] = 0, ["Cal"] = 0, ["Bob"] = 750, ["Dee"] = 200 }),
		["32-folded-aces-win-nothing"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 200, 100, "Kh Kd"),
			S("Bob", 1, 200, 100, "Qh Qd"),
			S("Cal", 2, 200, 100, "Ah Ad", folded: true),
			pots: [Pot(300, ["Ann", "Bob"], ["Cal"], ("Ann", 300))],
			payouts: new() { ["Ann"] = 300 },
			stacks: new() { ["Ann"] = 400, ["Bob"] = 100, ["Cal"] = 100 },
			foldedBest: "Ah Ad"),
		["40-two-tie-the-only-pot"] = Show(
			RoyalBoard, 0,
			S("Ann", 1, 80, 40, "2c 3d"),
			S("Bob", 2, 80, 40, "4c 5d"),
			S("Cal", 3, 80, 40, "6c 7d"),
			pots: [Pot(120, ["Ann", "Bob", "Cal"], [], ("Ann", 40), ("Bob", 40), ("Cal", 40))],
			payouts: new() { ["Ann"] = 40, ["Bob"] = 40, ["Cal"] = 40 },
			stacks: new() { ["Ann"] = 80, ["Bob"] = 80, ["Cal"] = 80 }),
		["42-three-way-odd-chip"] = Show(
			RoyalBoard, 0,
			S("Ann", 1, 33, 33, "2c 3d", allIn: true),
			S("Bob", 2, 33, 33, "4c 5d", allIn: true),
			S("Cal", 3, 33, 33, "6c 7d", allIn: true),
			S("Dee", 4, 1, 1, "8c 9d", folded: true),
			pots: [Pot(100, ["Ann", "Bob", "Cal"], ["Dee"], ("Ann", 34), ("Bob", 33), ("Cal", 33))],
			payouts: new() { ["Ann"] = 34, ["Bob"] = 33, ["Cal"] = 33 },
			stacks: new() { ["Ann"] = 34, ["Bob"] = 33, ["Cal"] = 33, ["Dee"] = 0 }),
		["45-split-main-different-side-winner"] = Show(
			QuietBoard, 0,
			S("Ann", 1, 50, 50, "Ah Ad", allIn: true),
			S("Bob", 2, 50, 50, "As Ac", allIn: true),
			S("Cal", 3, 400, 200, "Kh Kd"),
			S("Dee", 4, 400, 200, "Qh Qd"),
			S("Eve", 5, 1, 1, "7c 2d", folded: true),
			pots:
			[
				Pot(201, ["Ann", "Bob", "Cal", "Dee"], ["Eve"], ("Ann", 101), ("Bob", 100)),
				Pot(300, ["Cal", "Dee"], [], ("Cal", 300))
			],
			payouts: new() { ["Ann"] = 101, ["Bob"] = 100, ["Cal"] = 300 },
			stacks: new() { ["Ann"] = 101, ["Bob"] = 100, ["Cal"] = 500, ["Dee"] = 200, ["Eve"] = 0 }),
		["46-same-two-players-tie-main-and-side"] = Show(
			QuietBoard, 0,
			S("Ann", 1, 250, 200, "Ah Ad"),
			S("Bob", 2, 250, 200, "As Ac"),
			S("Cal", 3, 50, 50, "Kh Kd", allIn: true),
			pots:
			[
				Pot(150, ["Ann", "Bob", "Cal"], [], ("Ann", 75), ("Bob", 75)),
				Pot(300, ["Ann", "Bob"], [], ("Ann", 150), ("Bob", 150))
			],
			payouts: new() { ["Ann"] = 225, ["Bob"] = 225 },
			stacks: new() { ["Ann"] = 275, ["Bob"] = 275, ["Cal"] = 0 }),
		["47-odd-main-and-odd-side"] = Show(
			RoyalBoard, 0,
			S("Ann", 1, 2, 2, "2c 3d", allIn: true),
			S("Bob", 2, 10, 4, "4c 5d"),
			S("Cal", 3, 10, 4, "6c 7d"),
			S("Dee", 4, 3, 3, "8c 9d", folded: true),
			pots:
			[
				Pot(8, ["Ann", "Bob", "Cal"], ["Dee"], ("Ann", 3), ("Bob", 3), ("Cal", 2)),
				Pot(5, ["Bob", "Cal"], ["Dee"], ("Bob", 3), ("Cal", 2))
			],
			payouts: new() { ["Ann"] = 3, ["Bob"] = 6, ["Cal"] = 4 },
			stacks: new() { ["Ann"] = 3, ["Bob"] = 12, ["Cal"] = 10, ["Dee"] = 0 }),
		["50-tied-player-excluded-from-side"] = Show(
			QuietBoard, 0,
			S("Ann", 1, 50, 50, "Ah Ad", allIn: true),
			S("Bob", 2, 400, 200, "As Ac"),
			S("Cal", 3, 400, 200, "Qh Qd"),
			pots:
			[
				Pot(150, ["Ann", "Bob", "Cal"], [], ("Ann", 75), ("Bob", 75)),
				Pot(300, ["Bob", "Cal"], [], ("Bob", 300))
			],
			payouts: new() { ["Ann"] = 75, ["Bob"] = 375 },
			stacks: new() { ["Ann"] = 75, ["Bob"] = 575, ["Cal"] = 200 }),
		["51-kicker-not-category"] = Show(
			"2c 3d 4h 8s 9c", 0,
			S("Ann", 1, 200, 80, "Ah Kd"),
			S("Bob", 2, 200, 80, "As Qd"),
			pots: [Pot(160, ["Ann", "Bob"], [], ("Ann", 160))],
			payouts: new() { ["Ann"] = 160 },
			stacks: new() { ["Ann"] = 280, ["Bob"] = 120 }),
		["59-refund-and-pot-are-separate"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 150, 150, "Ah Ad", allIn: true),
			S("Bob", 1, 100, 100, "Kh Kd", allIn: true),
			pots: [Pot(200, ["Ann", "Bob"], [], ("Ann", 200))],
			payouts: new() { ["Ann"] = 200 },
			stacks: new() { ["Ann"] = 250, ["Bob"] = 0 }),
		["73-smallest-stack-has-the-nuts"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 30, 30, "Ah Ad", allIn: true),
			S("Bob", 1, 300, 100, "Kh Kd"),
			S("Cal", 2, 300, 100, "Qh Qd"),
			pots:
			[
				Pot(90, ["Ann", "Bob", "Cal"], [], ("Ann", 90)),
				Pot(140, ["Bob", "Cal"], [], ("Bob", 140))
			],
			payouts: new() { ["Ann"] = 90, ["Bob"] = 140 },
			stacks: new() { ["Ann"] = 90, ["Bob"] = 340, ["Cal"] = 200 }),
		["79-eligible-sets-pick-different-winners"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 40, 40, "Ah Ad", allIn: true),
			S("Bob", 1, 200, 100, "7c 2d"),
			S("Cal", 2, 200, 100, "Kh Kd"),
			pots:
			[
				Pot(120, ["Ann", "Bob", "Cal"], [], ("Ann", 120)),
				Pot(120, ["Bob", "Cal"], [], ("Cal", 120))
			],
			payouts: new() { ["Ann"] = 120, ["Cal"] = 120 },
			stacks: new() { ["Ann"] = 120, ["Bob"] = 100, ["Cal"] = 220 }),
		["76-board-ties-the-eligible-players"] = Show(
			RoyalBoard, 0,
			S("Ann", 1, 90, 30, "2c 3d", allIn: true),
			S("Bob", 2, 120, 60, "4c 5d"),
			S("Cal", 3, 120, 60, "6c 7d"),
			pots:
			[
				Pot(90, ["Ann", "Bob", "Cal"], [], ("Ann", 30), ("Bob", 30), ("Cal", 30)),
				Pot(60, ["Bob", "Cal"], [], ("Bob", 30), ("Cal", 30))
			],
			payouts: new() { ["Ann"] = 30, ["Bob"] = 60, ["Cal"] = 60 },
			stacks: new() { ["Ann"] = 90, ["Bob"] = 120, ["Cal"] = 120 }),
		["77-side-pot-kicker"] = Show(
			"2c 3d 4h 8s 9c", 0,
			S("Ann", 0, 40, 40, "6c 7d", allIn: true),
			S("Bob", 1, 200, 100, "Ah Kd"),
			S("Cal", 2, 200, 100, "As Qd"),
			pots:
			[
				Pot(120, ["Ann", "Bob", "Cal"], [], ("Bob", 120)),
				Pot(120, ["Bob", "Cal"], [], ("Bob", 120))
			],
			payouts: new() { ["Bob"] = 240 },
			stacks: new() { ["Ann"] = 0, ["Bob"] = 340, ["Cal"] = 100 }),
		["39-last-player"] = Show(
			QuietBoard, 0,
			S("Ann", 0, 200, 100, "Ah Ad"),
			S("Bob", 1, 80, 50, "Kd Qd", folded: true),
			S("Cal", 2, 40, 25, "7c 2d", folded: true),
			pots: [Pot(175, ["Ann"], ["Bob", "Cal"], ("Ann", 175))],
			payouts: new() { ["Ann"] = 175 },
			stacks: new() { ["Ann"] = 275, ["Bob"] = 30, ["Cal"] = 15 })
	};

	private static ShowdownCase Show(
		string board,
		int dealerSeat,
		SeatSpec[] seats,
		PotSpec[] pots,
		Dictionary<string, int> payouts,
		Dictionary<string, int> stacks,
		string? foldedBest = null) =>
		new(board, dealerSeat, seats, pots, payouts, stacks, foldedBest);

	private static ShowdownCase Show(
		string board,
		int dealerSeat,
		SeatSpec first,
		SeatSpec second,
		PotSpec[] pots,
		Dictionary<string, int> payouts,
		Dictionary<string, int> stacks,
		string? foldedBest = null) =>
		Show(board, dealerSeat, [first, second], pots, payouts, stacks, foldedBest);

	private static ShowdownCase Show(
		string board,
		int dealerSeat,
		SeatSpec first,
		SeatSpec second,
		SeatSpec third,
		PotSpec[] pots,
		Dictionary<string, int> payouts,
		Dictionary<string, int> stacks,
		string? foldedBest = null) =>
		Show(board, dealerSeat, [first, second, third], pots, payouts, stacks, foldedBest);

	private static ShowdownCase Show(
		string board,
		int dealerSeat,
		SeatSpec first,
		SeatSpec second,
		SeatSpec third,
		SeatSpec fourth,
		PotSpec[] pots,
		Dictionary<string, int> payouts,
		Dictionary<string, int> stacks,
		string? foldedBest = null) =>
		Show(board, dealerSeat, [first, second, third, fourth], pots, payouts, stacks, foldedBest);

	private static ShowdownCase Show(
		string board,
		int dealerSeat,
		SeatSpec first,
		SeatSpec second,
		SeatSpec third,
		SeatSpec fourth,
		SeatSpec fifth,
		PotSpec[] pots,
		Dictionary<string, int> payouts,
		Dictionary<string, int> stacks,
		string? foldedBest = null) =>
		Show(board, dealerSeat, [first, second, third, fourth, fifth], pots, payouts, stacks, foldedBest);

	private static SeatSpec S(
		string name,
		int seat,
		int starting,
		int contribution,
		string hole,
		bool allIn = false,
		bool folded = false) =>
		new(name, Guid.NewGuid(), seat, starting, contribution, hole, allIn, folded);

	private static PotSpec Pot(
		int amount,
		string[] eligible,
		string[] ineligible,
		params (string Name, int Amount)[] winners) =>
		new(amount, eligible, ineligible, winners);

	private sealed record SeatSpec(
		string Name,
		Guid Id,
		int Seat,
		int StartingStack,
		int Contribution,
		string Hole,
		bool AllIn,
		bool Folded);

	private sealed record PotSpec(
		int Amount,
		string[] Eligible,
		string[] Ineligible,
		(string Name, int Amount)[] Winners);

	private sealed record ShowdownCase(
		string Board,
		int DealerSeat,
		SeatSpec[] Seats,
		PotSpec[] Pots,
		Dictionary<string, int> Payouts,
		Dictionary<string, int> FinalStacks,
		string? FoldedHoleBeatsTheField);
}
