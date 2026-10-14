using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Games.Razz;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.E2E;

public class RazzFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in RazzFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(RazzFiveMaxCatalog.SeatCount, script.Holdings.Count);
			Assert.Equal(RazzFiveMaxCatalog.OpeningPot, script.OpeningPot);

			for (var dealer = 0; dealer < RazzFiveMaxCatalog.SeatCount; dealer++)
			{
				var trace = RazzDeckScript.Trace(dealer, script);
				Assert.Equal(52, trace.Deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(RazzFiveMaxCatalog.SeatCount, trace.Seats.Count);
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new RazzLedger(Enumerable.Repeat(1_000, RazzFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < RazzFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(RazzFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, RazzFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[RazzFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < RazzFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % RazzFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void HighDoor_SpadeKingBringsInAheadOfTheAceAndTheHeartKing()
	{
		var script = RazzFiveMaxCatalog.HighDoor;
		var bringIn = BringIn(script);

		Assert.Equal(HoldEmPosition.Button, bringIn);
		Assert.Equal(script.BringIn, bringIn);
		Assert.Equal("Ks", script.Holdings[HoldEmPosition.Button].Door);
		Assert.Equal("Ah", script.Holdings[HoldEmPosition.SmallBlind].Door);
	}

	[Fact]
	public void SuitedWheel_BeatsTheSixLow_AndAFlushDoesNotChangeTheLow()
	{
		var script = RazzFiveMaxCatalog.SuitedWheel;
		var wheel = Hand(script.Holdings[HoldEmPosition.SmallBlind]);
		var sixLow = Hand(script.Holdings[HoldEmPosition.BigBlind]);
		var rainbow = new RazzHand(
			[Parse("Ah"), Parse("2d")],
			[Parse("3c"), Parse("4s"), Parse("5h"), Parse("Kd")],
			[Parse("Qc")]);

		Assert.Equal(rainbow.Strength, wheel.Strength);
		Assert.True(wheel.Strength > sixLow.Strength);
		Assert.Equal("5-4-3-2-Ace", script.WinnerDescription);
		Assert.Equal(RazzFiveMaxCatalog.SmallBet, FourthBet(script).Amount);
		Assert.Equal(BestBoard(script, RazzStreet.Fourth), FourthBet(script).Actor);
	}

	[Fact]
	public void OpenPair_UnpairedLowActsFirst_AndFourthStreetUsesTheBigBet()
	{
		var script = RazzFiveMaxCatalog.OpenPair;
		var bet = FourthBet(script);
		var paired = script.Holdings[HoldEmPosition.BigBlind];

		Assert.Equal(HoldEmPosition.SmallBlind, bet.Actor);
		Assert.Equal(BestBoard(script, RazzStreet.Fourth), bet.Actor);
		Assert.Equal(RazzFiveMaxCatalog.BigBet, bet.Amount);
		Assert.True(RazzBoardOrder.HasOpenPair([(int)Parse(paired.Door).Symbol, (int)Parse(paired.Fourth!).Symbol]));
		Assert.True(Hand(script.Holdings[HoldEmPosition.SmallBlind]).Strength > Hand(paired).Strength);
		Assert.Equal("7-6-5-3-Ace", script.WinnerDescription);
	}

	[Fact]
	public void FifthStreetRaise_SixLowBeatsTheStraightSeven()
	{
		var script = RazzFiveMaxCatalog.FifthStreetRaise;
		var six = Hand(script.Holdings[HoldEmPosition.SmallBlind]);
		var seven = Hand(script.Holdings[HoldEmPosition.BigBlind]);
		var raise = script.Actions.Single(action => action.Kind == RazzActionKind.RaiseTo && action.Street == RazzStreet.Fifth);

		Assert.True(six.Strength > seven.Strength);
		Assert.Equal(RazzFiveMaxCatalog.BigBet * 2, raise.Amount);
		Assert.Equal("6-4-3-2-Ace", script.WinnerDescription);
		Assert.Equal(BestBoard(script, RazzStreet.Fifth), script.Actions.First(action => action.Street == RazzStreet.Fifth).Actor);
	}

	[Fact]
	public void HighCardsLose_EightLowBeatsTheKings_AndTheAceDoesNotBringIn()
	{
		var script = RazzFiveMaxCatalog.HighCardsLose;
		var eight = Hand(script.Holdings[HoldEmPosition.SmallBlind]);
		var kings = Hand(script.Holdings[HoldEmPosition.BigBlind]);

		Assert.Equal(HoldEmPosition.BigBlind, BringIn(script));
		Assert.Equal("Ad", script.Holdings[HoldEmPosition.Button].Door);
		Assert.True(eight.Strength > kings.Strength);
		Assert.Equal("8-6-5-4-2", script.WinnerDescription);
		Assert.Equal(BestBoard(script, RazzStreet.Fourth), script.Actions.First(action => action.Street == RazzStreet.Fourth).Actor);
	}

	private static RazzAction FourthBet(RazzHandScript script) =>
		script.Actions.Single(action => action.Street == RazzStreet.Fourth && action.Kind == RazzActionKind.Bet);

	private static HoldEmPosition BringIn(RazzHandScript script)
	{
		HoldEmPosition? selected = null;
		Card? selectedCard = null;
		foreach (var (position, holding) in script.Holdings)
		{
			var door = Parse(holding.Door);
			if (selectedCard is null || RazzBoardOrder.BringsInOver(door, selectedCard))
			{
				selected = position;
				selectedCard = door;
			}
		}

		return selected ?? throw new InvalidOperationException(script.Name);
	}

	private static HoldEmPosition BestBoard(RazzHandScript script, RazzStreet street)
	{
		HoldEmPosition? selected = null;
		long best = -1;
		foreach (var (position, holding) in script.Holdings)
		{
			if (RazzFiveMaxCatalog.FoldedBefore(script, position, street))
				continue;

			var strength = RazzBoardOrder.LowBoardStrength(UpCards(holding, street).Select(Parse).ToList());
			if (strength > best)
			{
				best = strength;
				selected = position;
			}
		}

		return selected ?? throw new InvalidOperationException(script.Name);
	}

	private static IReadOnlyList<string> UpCards(RazzHolding holding, RazzStreet street)
	{
		var cards = new List<string> { holding.Door };
		if (street >= RazzStreet.Fourth && holding.Fourth is not null)
			cards.Add(holding.Fourth);
		if (street >= RazzStreet.Fifth && holding.Fifth is not null)
			cards.Add(holding.Fifth);
		if (street >= RazzStreet.Sixth && holding.Sixth is not null)
			cards.Add(holding.Sixth);

		return cards;
	}

	private static RazzHand Hand(RazzHolding holding) =>
		new(
			[Parse(holding.FirstHole), Parse(holding.SecondHole)],
			[Parse(holding.Door), Parse(holding.Fourth!), Parse(holding.Fifth!), Parse(holding.Sixth!)],
			[Parse(holding.Seventh!)]);

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
