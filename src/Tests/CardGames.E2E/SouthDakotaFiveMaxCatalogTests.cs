using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.E2E;

public class SouthDakotaFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in SouthDakotaFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(SouthDakotaFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(3, script.Board.Count);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(5, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			for (var dealer = 0; dealer < SouthDakotaFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(SouthDakotaFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(SouthDakotaFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(SouthDakotaFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = SouthDakotaDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, SouthDakotaFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < SouthDakotaFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = SouthDakotaFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(4, SouthDakotaFiveMaxCatalog.Orbit.Count);
		Assert.Equal(5_000, ledger.Stacks.Sum());
	}

	[Fact]
	public void RoyalTrapCheckdown_SouthDakotaTripsBeatTheHoldEmRoyal()
	{
		var script = SouthDakotaFiveMaxCatalog.RoyalTrapCheckdown;
		var button = SouthDakota(script, HoldEmPosition.Button);
		var bigBlind = SouthDakota(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Trips, bigBlind.Type);
		Assert.Equal(HandType.HighCard, button.Type);
		Assert.True(bigBlind.Strength > button.Strength);
		Assert.Equal("Three of a kind, Deuces", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(bigBlind));

		var holdEmButton = HoldEm(script, HoldEmPosition.Button);
		var holdEmBigBlind = HoldEm(script, HoldEmPosition.BigBlind);
		Assert.Equal(HandType.StraightFlush, holdEmButton.Type);
		Assert.True(holdEmButton.Strength > holdEmBigBlind.Strength);
	}

	[Fact]
	public void TurnBetTrips_SouthDakotaUsesThreeAcesAndHoldEmUsesAPair()
	{
		var script = SouthDakotaFiveMaxCatalog.TurnBetTrips;
		var bigBlind = SouthDakota(script, HoldEmPosition.BigBlind);
		var holdEmBigBlind = HoldEm(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Trips, bigBlind.Type);
		Assert.Equal(HandType.OnePair, holdEmBigBlind.Type);
		Assert.Equal("Three of a kind, Aces", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(bigBlind));
		Assert.Equal("Pair of Aces", HandDescriptionFormatter.GetHandDescription(holdEmBigBlind));
		Assert.True(bigBlind.Strength > SouthDakota(script, HoldEmPosition.Button).Strength);
	}

	private static NebraskaHand SouthDakota(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static HoldemHand HoldEm(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static IReadOnlyCollection<Card> Cards(IReadOnlyList<string> cards) =>
		string.Join(' ', cards).ToCards();
}
