using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.E2E;

public class GoodBadUglyFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in GoodBadUglyFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(GoodBadUglyFiveMaxCatalog.SeatCount, script.Nets.Count);
			Assert.Equal(GoodBadUglyFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.Equal(3, script.Community.Count);

			for (var dealer = 0; dealer < GoodBadUglyFiveMaxCatalog.SeatCount; dealer++)
			{
				var opening = GoodBadUglyDeckScript.Build(dealer, script, communityFirst: false);
				var later = GoodBadUglyDeckScript.Build(dealer, script, communityFirst: true);
				var holeCount = GoodBadUglyFiveMaxCatalog.SeatCount * 4;
				Assert.Equal(52, opening.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(52, later.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(script.HoleCards[HoldEmPosition.SmallBlind], opening.Take(4));
				Assert.Equal(script.Community, opening.Skip(holeCount).Take(3));
				Assert.Equal(script.Community, later.Take(3));
				Assert.Equal(script.HoleCards[HoldEmPosition.SmallBlind], later.Skip(3).Take(4));
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new GoodBadUglyLedger(Enumerable.Repeat(1_000, GoodBadUglyFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < GoodBadUglyFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(GoodBadUglyFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, GoodBadUglyFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[GoodBadUglyFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < GoodBadUglyFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % GoodBadUglyFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void WildRoyal_DeuceMakesARoyalThatBeatsFourAces()
	{
		var script = GoodBadUglyFiveMaxCatalog.WildRoyal;
		var showdown = Showdown(script);

		Assert.Equal(HoldEmPosition.SmallBlind, showdown.Winner);
		Assert.Equal("Royal flush", showdown.Descriptions[HoldEmPosition.SmallBlind]);
		Assert.Equal("Four of a kind, Aces", showdown.Descriptions[HoldEmPosition.BigBlind]);
		Assert.Empty(showdown.Eliminated);
	}

	[Fact]
	public void BadDiscardsTheQueen_TurnsTheRoyalIntoAStraightAndFourAcesWins()
	{
		var script = GoodBadUglyFiveMaxCatalog.BadDiscardsTheQueen;
		var kept = Showdown(script, discardBad: false);
		var discarded = Showdown(script);

		Assert.Equal(HoldEmPosition.SmallBlind, kept.Winner);
		Assert.Equal("Royal flush", kept.Descriptions[HoldEmPosition.SmallBlind]);
		Assert.Equal(HoldEmPosition.BigBlind, discarded.Winner);
		Assert.Equal("Four of a kind, Aces", discarded.Descriptions[HoldEmPosition.BigBlind]);
		Assert.Equal("Straight to the Ace", discarded.Descriptions[HoldEmPosition.SmallBlind]);
		Assert.DoesNotContain("Qh", RemainingHole(script, HoldEmPosition.SmallBlind));
	}

	[Fact]
	public void UglyKillsTheRoyal_LiveStraightFlushWinsWhileTheDeadHandCanStillBeBet()
	{
		var script = GoodBadUglyFiveMaxCatalog.UglyKillsTheRoyal;
		var showdown = Showdown(script);

		Assert.Equal(HoldEmPosition.BigBlind, showdown.Winner);
		Assert.Equal("Straight flush to the Ten", showdown.Descriptions[HoldEmPosition.BigBlind]);
		Assert.Equal("Royal flush", showdown.Descriptions[HoldEmPosition.SmallBlind]);
		Assert.Contains(HoldEmPosition.SmallBlind, showdown.Eliminated);
		Assert.Contains(
			script.Actions,
			action => action.Actor == HoldEmPosition.SmallBlind && action.Kind == GoodBadUglyActionKind.Bet);
	}

	[Fact]
	public void AllDeadBestHandWins_DeadRoyalBeatsTheOtherDeadHand()
	{
		var script = GoodBadUglyFiveMaxCatalog.AllDeadBestHandWins;
		var showdown = Showdown(script);

		Assert.Equal(HoldEmPosition.SmallBlind, showdown.Winner);
		Assert.Equal("Royal flush", showdown.Descriptions[HoldEmPosition.SmallBlind]);
		Assert.Equal([HoldEmPosition.SmallBlind, HoldEmPosition.BigBlind], showdown.Eliminated);
		Assert.Contains(
			script.Actions,
			action => action.Kind == GoodBadUglyActionKind.RaiseTo);
	}

	private static IReadOnlyList<string> RemainingHole(GoodBadUglyHandScript script, HoldEmPosition position)
	{
		var bad = Parse(script.Community[1]);
		return script.HoleCards[position].Where(card => Parse(card).Value != bad.Value).ToArray();
	}

	private static ShowdownOutcome Showdown(GoodBadUglyHandScript script, bool discardBad = true)
	{
		var folded = script.Actions
			.Where(action => action.Kind == GoodBadUglyActionKind.Fold)
			.Select(action => action.Actor)
			.ToHashSet();
		var good = Parse(script.Community[0]);
		var bad = Parse(script.Community[1]);
		var ugly = Parse(script.Community[2]);
		var community = script.Community.Select(Parse).ToArray();
		var descriptions = new Dictionary<HoldEmPosition, string>();
		var strengths = new Dictionary<HoldEmPosition, long>();
		var eliminated = new List<HoldEmPosition>();

		foreach (var (position, hole) in script.HoleCards)
		{
			if (folded.Contains(position))
				continue;

			var remaining = hole.Select(Parse).Where(card => !discardBad || card.Value != bad.Value).ToList();
			if (remaining.Any(card => card.Value == ugly.Value))
				eliminated.Add(position);

			var hand = new GoodBadUglyHand(
				remaining.Concat(community).ToList(),
				[],
				[],
				good.Value,
				new GoodBadUglyWildCardRules());
			descriptions[position] = HandDescriptionFormatter.GetHandDescription(hand);
			strengths[position] = hand.Strength;
		}

		var anyLive = strengths.Keys.Any(position => !eliminated.Contains(position));
		var eligible = strengths.Keys.Where(position => !anyLive || !eliminated.Contains(position));
		var winner = eligible.OrderByDescending(position => strengths[position]).First();
		if (discardBad)
		{
			Assert.True(
				winner == script.Winner,
				string.Join(
					"; ",
					descriptions.Select(pair =>
						$"{pair.Key}={pair.Value} ({strengths[pair.Key]}) eliminated={eliminated.Contains(pair.Key)}")));
			if (!string.IsNullOrWhiteSpace(script.EvaluatedDescription))
				Assert.Equal(script.EvaluatedDescription, descriptions[winner]);
		}

		return new ShowdownOutcome(winner, descriptions, eliminated);
	}

	private static Card Parse(string code)
	{
		var suit = char.ToLowerInvariant(code[1]) switch
		{
			'h' => Suit.Hearts,
			'd' => Suit.Diamonds,
			's' => Suit.Spades,
			'c' => Suit.Clubs,
			_ => throw new ArgumentException(code)
		};
		var symbol = char.ToUpperInvariant(code[0]) switch
		{
			'A' => Symbol.Ace,
			'K' => Symbol.King,
			'Q' => Symbol.Queen,
			'J' => Symbol.Jack,
			'T' => Symbol.Ten,
			'2' => Symbol.Deuce,
			'3' => Symbol.Three,
			'4' => Symbol.Four,
			'5' => Symbol.Five,
			'6' => Symbol.Six,
			'7' => Symbol.Seven,
			'8' => Symbol.Eight,
			'9' => Symbol.Nine,
			_ => throw new ArgumentException(code)
		};
		return new Card(suit, symbol);
	}

	private sealed record ShowdownOutcome(
		HoldEmPosition Winner,
		IReadOnlyDictionary<HoldEmPosition, string> Descriptions,
		IReadOnlyList<HoldEmPosition> Eliminated);
}
