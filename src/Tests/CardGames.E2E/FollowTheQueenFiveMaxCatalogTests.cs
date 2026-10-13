using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.E2E;

public class FollowTheQueenFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in FollowTheQueenFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(FollowTheQueenFiveMaxCatalog.SeatCount, script.Holdings.Count);
			Assert.Equal(FollowTheQueenFiveMaxCatalog.OpeningPot, script.OpeningPot);

			for (var dealer = 0; dealer < FollowTheQueenFiveMaxCatalog.SeatCount; dealer++)
			{
				var trace = FollowTheQueenDeckScript.Trace(dealer, script);
				Assert.Equal(52, trace.Deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(FollowTheQueenFiveMaxCatalog.SeatCount, trace.Seats.Count);
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new FollowTheQueenLedger(Enumerable.Repeat(1_000, FollowTheQueenFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < FollowTheQueenFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(FollowTheQueenFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, FollowTheQueenFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[FollowTheQueenFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < FollowTheQueenFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % FollowTheQueenFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void DoorQueen_FollowRankAppearsOnTheCardAfterTheQueen()
	{
		var third = FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.DoorQueen)
			.Take(FollowTheQueenFiveMaxCatalog.SeatCount)
			.ToArray();
		var full = FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.DoorQueen);

		Assert.EndsWith("Qh", third[^1]);
		Assert.Null(FollowTheQueenDeckScript.FollowRank(third));
		Assert.Equal(FollowTheQueenFiveMaxCatalog.Kings, FollowTheQueenDeckScript.FollowRank(full));
	}

	[Fact]
	public void QueenReplaced_LaterQueenChangesTheFollowRank()
	{
		var third = FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.QueenReplaced)
			.Take(FollowTheQueenFiveMaxCatalog.SeatCount)
			.ToArray();
		var full = FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.QueenReplaced);

		Assert.Equal(FollowTheQueenFiveMaxCatalog.Deuces, FollowTheQueenDeckScript.FollowRank(third));
		Assert.Equal(FollowTheQueenFiveMaxCatalog.Kings, FollowTheQueenDeckScript.FollowRank(full));
	}

	[Fact]
	public void WildShowdown_FollowRankIsDeuces_AndTheOtherHandsHaveNoFollowRank()
	{
		Assert.Equal(
			FollowTheQueenFiveMaxCatalog.Deuces,
			FollowTheQueenDeckScript.FollowRank(
				FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.WildShowdown)));
		Assert.Null(FollowTheQueenDeckScript.FollowRank(
			FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.FoldToBringIn)));
		Assert.Null(FollowTheQueenDeckScript.FollowRank(
			FollowTheQueenDeckScript.FaceUpWalk(FollowTheQueenFiveMaxCatalog.BigBetRaise)));
		Assert.True(FollowTheQueenFiveMaxCatalog.WildShowdown.ShowsWild);
	}

	[Fact]
	public void WildShowdown_BigBlindBeatsUnderTheGunWithTheWildDeuce()
	{
		var script = FollowTheQueenFiveMaxCatalog.WildShowdown;
		var faceUp = FollowTheQueenDeckScript.FaceUpWalk(script).Select(Parse).ToList();
		var winner = script.Holdings[HoldEmPosition.BigBlind];
		var loser = script.Holdings[HoldEmPosition.UnderTheGun];

		var winnerHand = Hand(winner, faceUp);
		var loserHand = Hand(loser, faceUp);

		Assert.Contains(winnerHand.WildCards, card => card.Symbol == Symbol.Deuce);
		Assert.True(winnerHand.Strength > loserHand.Strength);
	}

	private static FollowTheQueenHand Hand(FollowTheQueenHolding holding, IReadOnlyList<Card> faceUp) =>
		new(
			[Parse(holding.FirstHole), Parse(holding.SecondHole)],
			[Parse(holding.Door), Parse(holding.Fourth!), Parse(holding.Fifth!), Parse(holding.Sixth!)],
			Parse(holding.Seventh!),
			faceUp);

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
}
