using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.E2E;

public class TollboothFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in TollboothFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(TollboothFiveMaxCatalog.SeatCount, script.Holdings.Count);
			Assert.Equal(TollboothFiveMaxCatalog.OpeningPot, script.OpeningPot);
			Assert.Equal(HoldEmPosition.BigBlind, script.BringIn);
			AssertPot(script);

			for (var dealer = 0; dealer < TollboothFiveMaxCatalog.SeatCount; dealer++)
			{
				var trace = TollboothDeckScript.Trace(dealer, script);
				Assert.Equal(52, trace.Deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(TollboothFiveMaxCatalog.SeatCount, trace.Seats.Count);
				foreach (var (position, holding) in script.Holdings)
				{
					Assert.Equal(holding.Cards, trace.Seats[position].Cards);
					Assert.Equal(holding.UpCards, trace.Seats[position].UpCards);
				}
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new TollboothLedger(Enumerable.Repeat(1_000, TollboothFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < TollboothFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(TollboothFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, TollboothFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[TollboothFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < TollboothFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % TollboothFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void DealerOnSeatTwo_ChoosesLowestRemainingSeatAfterTheOpener()
	{
		var seats = TollboothDeckScript.ChoiceSeats(dealerSeat: 2, folded: new HashSet<int>());

		Assert.Equal([3, 0, 1, 2, 4], seats);
	}

	[Fact]
	public void Offers_FollowTheServerChoiceOrderForTheOrbitDealer()
	{
		for (var hand = 0; hand < TollboothFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = TollboothFiveMaxCatalog.Orbit[hand];
			foreach (var street in Enum.GetValues<TollboothStreet>())
			{
				var actors = script.Steps
					.Where(step => step.Kind == TollboothStepKind.Choose && step.Street == street)
					.Select(step => step.Actor)
					.ToArray();
				if (actors.Length == 0)
					continue;

				var folded = script.Steps
					.Where(step => step.Action == TollboothActionKind.Fold && step.Street < street)
					.Select(step => step.Actor)
					.ToHashSet();
				Assert.Equal(TollboothDeckScript.ChoiceOrder(hand, folded), actors);
			}
		}
	}

	[Fact]
	public void LaterStreets_OpenOnTheBestVisibleBoard()
	{
		for (var hand = 0; hand < TollboothFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = TollboothFiveMaxCatalog.Orbit[hand];
			foreach (var street in new[]
			         {
				         TollboothStreet.Fourth, TollboothStreet.Fifth, TollboothStreet.Sixth, TollboothStreet.Seventh
			         })
			{
				var opener = script.Steps.FirstOrDefault(step =>
					step.Kind == TollboothStepKind.Bet && step.Street == street);
				if (opener is null)
					continue;

				var folded = script.Steps
					.Where(step => step.Action == TollboothActionKind.Fold && step.Street < street)
					.Select(step => step.Actor)
					.ToHashSet();
				var best = script.Holdings.Keys
					.Where(position => !folded.Contains(position))
					.Select(position => (
						Position: position,
						Seat: HoldEmSeats.Index(TollboothFiveMaxCatalog.SeatCount, hand, position),
						Strength: TollboothDeckScript.VisibleStrength(Visible(script.Holdings[position], street))))
					.OrderByDescending(candidate => candidate.Strength)
					.ThenBy(candidate => candidate.Seat)
					.First();

				Assert.Equal(best.Position, opener.Actor);
			}
		}
	}

	[Fact]
	public void PaidChoices_NearestTakeMakesTheOldFurthestCardNearest()
	{
		var trace = TollboothDeckScript.Trace(dealerSeat: 2, TollboothFiveMaxCatalog.PaidChoices);
		var nearest = trace.Choices[0];

		Assert.Equal(TollboothSource.Nearest, nearest.Source);
		Assert.Equal(TollboothFiveMaxCatalog.Ante, nearest.Cost);
		Assert.Contains(trace.Choices, choice => choice.Source == TollboothSource.Furthest && choice.Cost == 0);
		Assert.Contains(
			trace.Choices,
			choice => choice.Source == TollboothSource.Deck && choice.Cost == TollboothFiveMaxCatalog.Ante * 2);
		Assert.Equal(nearest.FurthestBefore, nearest.NearestAfter);
		Assert.NotEqual(nearest.NearestBefore, nearest.NearestAfter);
		Assert.Equal(85, TollboothFiveMaxCatalog.PaidChoices.AwardedAmount);
	}

	[Fact]
	public void FreeCheckdown_ChargesNothingAndKeepsSeventhStreetFaceDown()
	{
		var trace = TollboothDeckScript.Trace(dealerSeat: 1, TollboothFiveMaxCatalog.FreeCheckdown);

		Assert.All(trace.Choices, choice => Assert.Equal(0, choice.Cost));
		Assert.Equal(TollboothFiveMaxCatalog.CalledPot, TollboothFiveMaxCatalog.FreeCheckdown.AwardedAmount);
		AssertFaceDownSeventh(trace);
	}

	[Fact]
	public void Showdowns_AwardTheBestFiveCardHand()
	{
		foreach (var script in TollboothFiveMaxCatalog.Orbit.Where(script => !script.WonByFold))
		{
			var winner = script.Holdings
				.Select(pair => (pair.Key, Hand: Stud(pair.Value.Cards)))
				.OrderByDescending(pair => pair.Hand.Strength)
				.First();

			Assert.Equal(script.Winner, winner.Key);
		}
	}

	[Fact]
	public void DisplayCards_WouldChangeTheWinnerIfTheyCounted()
	{
		var script = TollboothFiveMaxCatalog.DisplayExcluded;
		var without = script.Holdings.ToDictionary(pair => pair.Key, pair => Stud(pair.Value.Cards));
		var withDisplay = script.Holdings.ToDictionary(
			pair => pair.Key,
			pair => Stud([.. pair.Value.Cards, "As", "Ad"]));

		Assert.Equal(script.Winner, Best(without));
		Assert.Equal(HoldEmPosition.UnderTheGun, Best(withDisplay));
		Assert.NotEqual(Best(without), Best(withDisplay));
	}

	private static void AssertPot(TollboothHandScript script)
	{
		var pot = script.OpeningPot;
		int? last = null;
		foreach (var step in script.Steps)
		{
			if (step.Kind == TollboothStepKind.Choose)
			{
				Assert.NotNull(step.Source);
				Assert.Equal(pot + TollboothDeckScript.Cost(step.Source.Value), step.PotAfter);
				pot = step.PotAfter!.Value;
				last = pot;
				continue;
			}

			if (step.PotAfter is int next)
			{
				pot = next;
				last = next;
			}
		}

		var livePot = last ?? script.OpeningPot;
		Assert.InRange(script.AwardedAmount, 1, livePot);
	}

	private static void AssertFaceDownSeventh(TollboothDealTrace trace)
	{
		Assert.Contains(trace.Choices, choice => choice.Street == TollboothStreet.Seventh);
		Assert.All(
			trace.Choices.Where(choice => choice.Street == TollboothStreet.Seventh),
			choice => Assert.True(choice.FaceDown));
		Assert.All(
			trace.Choices.Where(choice => choice.Street != TollboothStreet.Seventh),
			choice => Assert.False(choice.FaceDown));
		foreach (var seat in trace.Seats.Values)
			Assert.All(seat.UpCards, card => Assert.DoesNotContain(card, SeventhCards(trace)));
	}

	private static IEnumerable<string> SeventhCards(TollboothDealTrace trace) =>
		trace.Choices
			.Where(choice => choice.Street == TollboothStreet.Seventh)
			.Select(choice => choice.Received);

	private static IReadOnlyList<string> Visible(TollboothHolding holding, TollboothStreet street)
	{
		var cards = new List<string> { holding.Door };
		if (street >= TollboothStreet.Fourth && holding.Fourth is not null)
			cards.Add(holding.Fourth);
		if (street >= TollboothStreet.Fifth && holding.Fifth is not null)
			cards.Add(holding.Fifth);
		if (street >= TollboothStreet.Sixth && holding.Sixth is not null)
			cards.Add(holding.Sixth);
		return cards;
	}

	private static HoldEmPosition Best(IReadOnlyDictionary<HoldEmPosition, StudHand> hands) =>
		hands.OrderByDescending(pair => pair.Value.Strength).First().Key;

	private static StudHand Stud(IReadOnlyList<string> cards) =>
		new(cards.Select(Parse).ToArray(), [], []);

	private static Card Parse(string code)
	{
		var suit = char.ToLowerInvariant(code[^1]) switch
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
