using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.E2E;

public class BobBarkerFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in BobBarkerFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(BobBarkerFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.Equal(BobBarkerFiveMaxCatalog.SeatCount, script.Showcases.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.Equal(script.WonByFold, script.ShowcaseWinner is null);

			var dealt = new HashSet<string>(StringComparer.Ordinal);
			foreach (var (position, holes) in script.HoleCards)
			{
				Assert.Equal(5, holes.Count);
				Assert.Contains(script.Showcases[position], holes);
				foreach (var card in holes)
					Assert.True(dealt.Add(card), $"Hand '{script.Name}' repeats {card}.");
			}

			Assert.True(dealt.Add(script.DealerCard), $"Hand '{script.Name}' repeats the dealer card.");
			foreach (var card in script.Board)
				Assert.True(dealt.Add(card), $"Hand '{script.Name}' repeats {card}.");

			for (var dealer = 0; dealer < BobBarkerFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(BobBarkerFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(BobBarkerFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(BobBarkerFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = BobBarkerDeckScript.Build(dealer, holes, script.DealerCard, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(31, deck.Take(31).Distinct(StringComparer.Ordinal).Count());
			}

			var ledger = new BobBarkerLedger(Enumerable.Repeat(1_000, BobBarkerFiveMaxCatalog.SeatCount).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void Orbit_AssignsEachSeatAsDealerSmallBlindAndBigBlindOnce()
	{
		var dealerCounts = new int[BobBarkerFiveMaxCatalog.SeatCount];
		var smallBlindCounts = new int[BobBarkerFiveMaxCatalog.SeatCount];
		var bigBlindCounts = new int[BobBarkerFiveMaxCatalog.SeatCount];

		for (var hand = 0; hand < BobBarkerFiveMaxCatalog.SeatCount; hand++)
		{
			var dealer = hand % BobBarkerFiveMaxCatalog.SeatCount;
			dealerCounts[dealer]++;
			smallBlindCounts[HoldEmSeats.Index(BobBarkerFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.SmallBlind)]++;
			bigBlindCounts[HoldEmSeats.Index(BobBarkerFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.BigBlind)]++;
		}

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
		Assert.All(smallBlindCounts, count => Assert.Equal(1, count));
		Assert.All(bigBlindCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new BobBarkerLedger(Enumerable.Repeat(1_000, BobBarkerFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < BobBarkerFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(BobBarkerFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, BobBarkerFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void ContestedShowdowns_MatchTheScriptedHighHandAndShowcase()
	{
		AssertHighHandBeats(
			BobBarkerFiveMaxCatalog.CheckdownSplit,
			HoldEmPosition.Button,
			HoldEmPosition.BigBlind);
		AssertHighHandBeats(
			BobBarkerFiveMaxCatalog.RiverRaiseNoQualifier,
			HoldEmPosition.Button,
			HoldEmPosition.BigBlind);
		AssertHighHandBeats(
			BobBarkerFiveMaxCatalog.AceDealerTarget,
			HoldEmPosition.Button,
			HoldEmPosition.BigBlind);

		AssertShowcase(BobBarkerFiveMaxCatalog.CheckdownSplit, HoldEmPosition.BigBlind, rolledIntoHighHand: false);
		AssertShowcase(BobBarkerFiveMaxCatalog.RiverRaiseNoQualifier, HoldEmPosition.Button, rolledIntoHighHand: true);
		AssertShowcase(BobBarkerFiveMaxCatalog.AceDealerTarget, HoldEmPosition.BigBlind, rolledIntoHighHand: false);
	}

	private static void AssertHighHandBeats(
		BobBarkerHandScript script,
		HoldEmPosition winner,
		HoldEmPosition loser)
	{
		var winnerStrength = OmahaStrength(script, winner);
		var loserStrength = OmahaStrength(script, loser);
		Assert.True(
			winnerStrength > loserStrength,
			$"Hand '{script.Name}' expected {winner} to beat {loser}, but {winnerStrength} is not greater than {loserStrength}.");
	}

	private static long OmahaStrength(BobBarkerHandScript script, HoldEmPosition position)
	{
		var showcase = script.Showcases[position];
		var holes = script.HoleCards[position]
			.Where(card => !string.Equals(card, showcase, StringComparison.Ordinal))
			.Select(Parse)
			.ToArray();
		var board = script.Board.Select(Parse).ToArray();
		return new BobBarkerHand(holes, board).Strength;
	}

	private static void AssertShowcase(BobBarkerHandScript script, HoldEmPosition winner, bool rolledIntoHighHand)
	{
		Assert.Equal(winner, script.ShowcaseWinner);
		Assert.Equal(rolledIntoHighHand, script.ShowcaseRolledIntoHighHand);
		Assert.Equal(rolledIntoHighHand, script.ShowcaseWinner == script.Winner);

		var dealerValue = script.DealerCard.StartsWith('A') ? 14 : RankValue(script.DealerCard);
		var live = script.Actions
			.Where(action => action.Kind == HoldEmActionKind.Fold)
			.Select(action => action.Actor)
			.ToHashSet();
		var qualified = script.Showcases
			.Where(pair => !live.Contains(pair.Key))
			.Select(pair => (pair.Key, Value: ShowcaseValue(pair.Value, dealerValue)))
			.Where(pair => pair.Value <= dealerValue)
			.ToArray();

		if (rolledIntoHighHand)
		{
			Assert.Empty(qualified);
			return;
		}

		var best = qualified.Max(pair => pair.Value);
		var winners = qualified.Where(pair => pair.Value == best).Select(pair => pair.Key).ToArray();
		Assert.Equal([winner], winners);
	}

	private static int ShowcaseValue(string card, int dealerValue)
	{
		var rank = RankValue(card);
		if (rank == 14)
			return dealerValue == 14 ? 14 : 1;

		return rank;
	}

	private static int RankValue(string card)
	{
		return char.ToUpperInvariant(card[0]) switch
		{
			'A' => 14,
			'K' => 13,
			'Q' => 12,
			'J' => 11,
			'T' => 10,
			_ => card[0] - '0'
		};
	}

	private static Card Parse(string code)
	{
		var suit = code[1] switch
		{
			'h' => Suit.Hearts,
			'd' => Suit.Diamonds,
			's' => Suit.Spades,
			'c' => Suit.Clubs,
			_ => throw new ArgumentException($"'{code}' is not a valid card.")
		};
		var symbol = char.ToUpperInvariant(code[0]) switch
		{
			'A' => Symbol.Ace,
			'K' => Symbol.King,
			'Q' => Symbol.Queen,
			'J' => Symbol.Jack,
			'T' => Symbol.Ten,
			'9' => Symbol.Nine,
			'8' => Symbol.Eight,
			'7' => Symbol.Seven,
			'6' => Symbol.Six,
			'5' => Symbol.Five,
			'4' => Symbol.Four,
			'3' => Symbol.Three,
			'2' => Symbol.Deuce,
			_ => throw new ArgumentException($"'{code}' is not a valid card.")
		};
		return new Card(suit, symbol);
	}
}
