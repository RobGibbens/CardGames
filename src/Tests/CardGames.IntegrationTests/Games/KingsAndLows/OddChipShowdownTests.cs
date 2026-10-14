using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Games.KingsAndLows;

public class OddChipShowdownTests : IntegrationTestBase
{
	[Fact]
	public async Task ThreeWayTie_PaysOddChipsClockwiseFromTheDealer()
	{
		var gameId = await SeedShowdownAsync(new Dictionary<string, string?>
		{
			["Alice"] = QueenHighStraightSpades,
			["Bob"] = QueenHighStraightHearts,
			["Carol"] = QueenHighStraightDiamonds
		});

		var first = await Mediator.Send(new PerformShowdownCommand(gameId));
		first.IsT0.Should().BeTrue(first.IsT1 ? first.AsT1.Message : string.Empty);

		await using (var seen = GetFreshDbContext())
		{
			var stacks = await StacksAsync(seen, gameId);
			stacks["Alice"].Should().Be(102);
			stacks["Bob"].Should().Be(101);
			stacks["Carol"].Should().Be(102);
			stacks.Values.Sum().Should().Be(305);

			var pots = await seen.Pots.Where(pot => pot.GameId == gameId).ToListAsync();
			pots.Should().ContainSingle();
			pots[0].HandNumber.Should().Be(1);
			pots[0].IsAwarded.Should().BeTrue();
		}

		var second = await Mediator.Send(new PerformShowdownCommand(gameId));
		second.IsT0.Should().BeTrue(second.IsT1 ? second.AsT1.Message : string.Empty);

		await using (var replayed = GetFreshDbContext())
		{
			var stacks = await StacksAsync(replayed, gameId);
			stacks["Alice"].Should().Be(102);
			stacks["Bob"].Should().Be(101);
			stacks["Carol"].Should().Be(102);
		}
	}

	[Fact]
	public async Task TwoWayTie_MatchesTheLoserForTheWholePot()
	{
		var gameId = await SeedShowdownAsync(new Dictionary<string, string?>
		{
			["Alice"] = QueenHighStraightSpades,
			["Bob"] = BobLosingHand,
			["Carol"] = QueenHighStraightDiamonds
		});

		var result = await Mediator.Send(new PerformShowdownCommand(gameId));
		result.IsT0.Should().BeTrue(result.IsT1 ? result.AsT1.Message : string.Empty);

		await using var seen = GetFreshDbContext();
		var stacks = await StacksAsync(seen, gameId);
		stacks["Alice"].Should().Be(102);
		stacks["Bob"].Should().Be(95);
		stacks["Carol"].Should().Be(103);

		var handTwo = await seen.Pots.SingleAsync(pot => pot.GameId == gameId && pot.HandNumber == 2);
		handTwo.PotType.Should().Be(PotType.Main);
		handTwo.IsAwarded.Should().BeFalse();
		handTwo.Amount.Should().Be(5);
		(stacks.Values.Sum() + handTwo.Amount).Should().Be(305);
	}

	private async Task<Guid> SeedShowdownAsync(IReadOnlyDictionary<string, string?> hands)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "KINGSANDLOWS", 3, startingChips: 100, ante: 0);

		var names = new[] { "Alice", "Bob", "Carol" };
		var seats = new[] { 0, 3, 5 };
		for (var index = 0; index < names.Length; index++)
		{
			setup.Players[index].Name = names[index];
			setup.GamePlayers[index].SeatPosition = seats[index];
			setup.GamePlayers[index].ChipStack = 100;
			setup.GamePlayers[index].HasFolded = false;
			setup.GamePlayers[index].Status = GamePlayerStatus.Active;
		}

		var game = setup.Game;
		game.DealerPosition = 3;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.CurrentHandNumber = 1;
		game.Status = GameStatus.InProgress;

		var dealtCards = await DbContext.GameCards.Where(card => card.GameId == game.Id).ToListAsync();
		DbContext.GameCards.RemoveRange(dealtCards);

		var now = DateTimeOffset.UtcNow;
		foreach (var gamePlayer in setup.GamePlayers)
		{
			if (!hands.TryGetValue(gamePlayer.Player.Name, out var hand) || hand is null)
				continue;

			var dealOrder = 1;
			foreach (var token in hand.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				var (suit, symbol) = ParseCard(token);
				DbContext.GameCards.Add(new GameCard
				{
					GameId = game.Id,
					GamePlayerId = gamePlayer.Id,
					HandNumber = 1,
					DealOrder = dealOrder++,
					Location = CardLocation.Hand,
					IsDiscarded = false,
					IsVisible = false,
					Suit = suit,
					Symbol = symbol,
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
			}
		}

		DbContext.Pots.Add(new Pot
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 5,
			IsAwarded = false,
			CreatedAt = now
		});

		await DbContext.SaveChangesAsync();
		return game.Id;
	}

	private static async Task<Dictionary<string, int>> StacksAsync(CardsDbContext context, Guid gameId)
	{
		return await context.GamePlayers
			.Include(gamePlayer => gamePlayer.Player)
			.Where(gamePlayer => gamePlayer.GameId == gameId)
			.ToDictionaryAsync(gamePlayer => gamePlayer.Player.Name, gamePlayer => gamePlayer.ChipStack);
	}

	private static (CardSuit Suit, CardSymbol Symbol) ParseCard(string token)
	{
		var suit = token[^1] switch
		{
			's' => CardSuit.Spades,
			'h' => CardSuit.Hearts,
			'd' => CardSuit.Diamonds,
			'c' => CardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown suit")
		};
		var symbol = token[..^1] switch
		{
			"2" => CardSymbol.Deuce,
			"3" => CardSymbol.Three,
			"4" => CardSymbol.Four,
			"5" => CardSymbol.Five,
			"7" => CardSymbol.Seven,
			"8" => CardSymbol.Eight,
			"9" => CardSymbol.Nine,
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			_ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown symbol")
		};
		return (suit, symbol);
	}

	private const string QueenHighStraightSpades = "Qs Jd 9h 8c 7s";
	private const string QueenHighStraightHearts = "Qh Jc 9d 8s 7h";
	private const string QueenHighStraightDiamonds = "Qd Js 9c 8h 7d";
	private const string BobLosingHand = "2s 3h 4d 5c 9h";
}
