using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.E2E;

public class SevenCardStudFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in SevenCardStudFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(SevenCardStudFiveMaxCatalog.SeatCount, script.Holdings.Count);
			Assert.Equal(SevenCardStudFiveMaxCatalog.OpeningPot, script.OpeningPot);
			Assert.Equal(LowestDoor(script), script.BringIn);

			for (var dealer = 0; dealer < SevenCardStudFiveMaxCatalog.SeatCount; dealer++)
			{
				var trace = SevenCardStudDeckScript.Trace(dealer, script);
				Assert.Equal(52, trace.Deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(SevenCardStudFiveMaxCatalog.SeatCount, trace.Seats.Count);
				Assert.Equal(script.Holdings[HoldEmPosition.SmallBlind].FirstHole, trace.Deck[0]);
				Assert.Equal(script.Holdings[HoldEmPosition.SmallBlind].SecondHole, trace.Deck[1]);
				Assert.Equal(script.Holdings[HoldEmPosition.SmallBlind].Door, trace.Deck[2]);
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new SevenCardStudLedger(
			Enumerable.Repeat(1_000, SevenCardStudFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < SevenCardStudFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(SevenCardStudFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, SevenCardStudFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[SevenCardStudFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < SevenCardStudFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % SevenCardStudFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void SuitDecidesTheBringIn_ClubsBeatDiamonds()
	{
		var script = SevenCardStudFiveMaxCatalog.SuitDecidesTheBringIn;

		Assert.Equal("2c", script.Holdings[HoldEmPosition.BigBlind].Door);
		Assert.Equal("2d", script.Holdings[HoldEmPosition.SmallBlind].Door);
		Assert.Equal(HoldEmPosition.BigBlind, script.BringIn);
		Assert.Equal(HoldEmPosition.UnderTheGun, script.Actions[0].Actor);
		Assert.True(script.WonByFold);
	}

	[Fact]
	public void CompleteTheBringIn_RaisesToTheSmallBet()
	{
		var raise = SevenCardStudFiveMaxCatalog.CompleteTheBringIn.Actions[0];

		Assert.Equal(SevenCardStudActionKind.RaiseTo, raise.Kind);
		Assert.Equal(SevenCardStudStreet.Third, raise.Street);
		Assert.Equal(SevenCardStudFiveMaxCatalog.SmallBet, raise.Amount);
		Assert.NotEqual(SevenCardStudFiveMaxCatalog.BringInAmount, raise.Amount);
	}

	[Fact]
	public void PairOnFourthStreet_ActsBeforeTheSeatLeftOfTheDealer_AndBetsTheSmallBet()
	{
		var script = SevenCardStudFiveMaxCatalog.PairActsForTheSmallBet;
		var fourth = script.Actions.Where(action => action.Street == SevenCardStudStreet.Fourth).ToArray();

		Assert.Equal(SevenCardStudActionKind.Bet, fourth[0].Kind);
		Assert.Equal(HoldEmPosition.Cutoff, fourth[0].Actor);
		Assert.NotEqual(HoldEmPosition.SmallBlind, fourth[0].Actor);
		Assert.Equal(SevenCardStudFiveMaxCatalog.SmallBet, fourth[0].Amount);
		Assert.NotEqual(SevenCardStudFiveMaxCatalog.BigBet, fourth[0].Amount);
		Assert.Equal(HoldEmPosition.SmallBlind, fourth[1].Actor);
	}

	[Fact]
	public void Showdown_SpadeActsBeforeHeart_AndTheDownCardMakesTheFlush()
	{
		var script = SevenCardStudFiveMaxCatalog.BestFiveShowdown;
		var fourth = script.Actions.Where(action => action.Street == SevenCardStudStreet.Fourth).ToArray();
		var fifthBet = script.Actions.Single(action =>
			action.Street == SevenCardStudStreet.Fifth && action.Kind == SevenCardStudActionKind.Bet);
		var winner = script.Holdings[HoldEmPosition.BigBlind];
		var loser = script.Holdings[HoldEmPosition.SmallBlind];

		Assert.Equal(HoldEmPosition.BigBlind, fourth[0].Actor);
		Assert.Equal(HoldEmPosition.SmallBlind, fourth[1].Actor);
		Assert.Equal("As", winner.Door);
		Assert.Equal("Ah", loser.Door);
		Assert.Equal(SevenCardStudFiveMaxCatalog.BigBet, fifthBet.Amount);

		var trace = SevenCardStudDeckScript.Trace(0, script);
		Assert.Contains(winner.Seventh, trace.Seats[HoldEmPosition.BigBlind].Cards);
		Assert.DoesNotContain(winner.Seventh, trace.Seats[HoldEmPosition.BigBlind].UpCards);
		Assert.True(Hand(winner).Strength > Hand(loser).Strength);
		Assert.False(script.WonByFold);
	}

	[Fact]
	public void SixthStreet_BetsTheBigBet()
	{
		var bet = SevenCardStudFiveMaxCatalog.SixthStreetBigBet.Actions.Single(action =>
			action.Kind == SevenCardStudActionKind.Bet);

		Assert.Equal(SevenCardStudStreet.Sixth, bet.Street);
		Assert.Equal(HoldEmPosition.UnderTheGun, bet.Actor);
		Assert.Equal(SevenCardStudFiveMaxCatalog.BigBet, bet.Amount);
	}

	[Fact]
	public void AggressiveActions_UseTheStreetLimit()
	{
		foreach (var script in SevenCardStudFiveMaxCatalog.Orbit)
		{
			foreach (var action in script.Actions)
			{
				if (action.Kind is not (SevenCardStudActionKind.Bet or SevenCardStudActionKind.RaiseTo))
					continue;

				var limit = action.Street is SevenCardStudStreet.Third or SevenCardStudStreet.Fourth
					? SevenCardStudFiveMaxCatalog.SmallBet
					: SevenCardStudFiveMaxCatalog.BigBet;
				Assert.NotNull(action.Amount);
				Assert.True(action.Amount >= limit);
				Assert.Equal(0, action.Amount % limit);
			}
		}
	}

	private static HoldEmPosition LowestDoor(SevenCardStudHandScript script)
	{
		HoldEmPosition lowest = default;
		var lowestRank = int.MaxValue;
		var lowestSuit = int.MaxValue;
		var found = false;
		foreach (var (position, holding) in script.Holdings)
		{
			var rank = Rank(holding.Door);
			var suit = SuitRank(holding.Door);
			if (found && (rank > lowestRank || (rank == lowestRank && suit >= lowestSuit)))
				continue;

			found = true;
			lowest = position;
			lowestRank = rank;
			lowestSuit = suit;
		}

		return lowest;
	}

	private static int Rank(string card) => char.ToUpperInvariant(card[0]) switch
	{
		'A' => 14,
		'K' => 13,
		'Q' => 12,
		'J' => 11,
		'T' => 10,
		_ => card[0] - '0'
	};

	private static int SuitRank(string card) => char.ToLowerInvariant(card[1]) switch
	{
		'c' => 0,
		'd' => 1,
		'h' => 2,
		's' => 3,
		_ => throw new ArgumentException(card)
	};

	private static SevenCardStudHand Hand(SevenCardStudHolding holding) =>
		new(
			[Parse(holding.FirstHole), Parse(holding.SecondHole)],
			[Parse(holding.Door), Parse(holding.Fourth!), Parse(holding.Fifth!), Parse(holding.Sixth!)],
			Parse(holding.Seventh!));

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
