using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.E2E;

public class NebraskaFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in NebraskaFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(NebraskaFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(5, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			for (var dealer = 0; dealer < NebraskaFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(NebraskaFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(NebraskaFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(NebraskaFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = NebraskaDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}

			var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, NebraskaFiveMaxCatalog.SeatCount).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void Orbit_AssignsEachSeatAsDealerSmallBlindAndBigBlindOnce()
	{
		var dealerCounts = new int[NebraskaFiveMaxCatalog.SeatCount];
		var smallBlindCounts = new int[NebraskaFiveMaxCatalog.SeatCount];
		var bigBlindCounts = new int[NebraskaFiveMaxCatalog.SeatCount];

		for (var hand = 0; hand < NebraskaFiveMaxCatalog.SeatCount; hand++)
		{
			var dealer = hand % NebraskaFiveMaxCatalog.SeatCount;
			dealerCounts[dealer]++;
			smallBlindCounts[HoldEmSeats.Index(NebraskaFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.SmallBlind)]++;
			bigBlindCounts[HoldEmSeats.Index(NebraskaFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.BigBlind)]++;
		}

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
		Assert.All(smallBlindCounts, count => Assert.Equal(1, count));
		Assert.All(bigBlindCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, NebraskaFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < NebraskaFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(NebraskaFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, NebraskaFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void BoardRoyal_UsesThreeHoleCards_AndDoesNotChopTheBoard()
	{
		var script = NebraskaFiveMaxCatalog.BoardRoyalDoesNotPlay;
		var button = Nebraska(script, HoldEmPosition.Button);
		var bigBlind = Nebraska(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Straight, button.Type);
		Assert.Equal(HandType.Flush, bigBlind.Type);
		Assert.True(bigBlind.Strength > button.Strength);
		Assert.Equal("Ace high flush", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(bigBlind));
		Assert.Equal("Straight to the Ace", HandDescriptionFormatter.GetHandDescription(button));

		var holdEmButton = HoldEm(script, HoldEmPosition.Button);
		var holdEmBigBlind = HoldEm(script, HoldEmPosition.BigBlind);
		Assert.Equal(holdEmButton.Strength, holdEmBigBlind.Strength);
		Assert.Equal(HandType.StraightFlush, holdEmBigBlind.Type);
	}

	[Fact]
	public void RiverRaise_QuadsBeatThreeKings()
	{
		var script = NebraskaFiveMaxCatalog.RiverRaise;
		var button = Nebraska(script, HoldEmPosition.Button);
		var bigBlind = Nebraska(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Quads, button.Type);
		Assert.Equal(HandType.Trips, bigBlind.Type);
		Assert.True(button.Strength > bigBlind.Strength);
		Assert.Equal("Four of a kind, Aces", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));
	}

	[Fact]
	public void SplitStraight_BothShowdownHandsAreTheSameStraight_AndTheOddChipGoesToTheBigBlind()
	{
		var script = NebraskaFiveMaxCatalog.SplitStraight;
		var button = Nebraska(script, HoldEmPosition.Button);
		var bigBlind = Nebraska(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Straight, button.Type);
		Assert.Equal(button.Strength, bigBlind.Strength);
		Assert.Equal("Straight to the Ace", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(bigBlind));

		for (var dealer = 0; dealer < NebraskaFiveMaxCatalog.SeatCount; dealer++)
		{
			var bigBlindSeat = HoldEmSeats.Index(NebraskaFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.BigBlind);
			var buttonSeat = HoldEmSeats.Index(NebraskaFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.Button);
			var firstPaid = new[] { bigBlindSeat, buttonSeat }
				.OrderBy(seat => seat > dealer ? 0 : 1)
				.ThenBy(seat => seat)
				.First();

			Assert.Equal(bigBlindSeat, firstPaid);
		}
	}

	private static NebraskaHand Nebraska(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static HoldemHand HoldEm(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static IReadOnlyCollection<Card> Cards(IReadOnlyList<string> cards) =>
		string.Join(' ', cards).ToCards();
}
