using CardGames.Core.French.Cards.Extensions;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.E2E;

public class KlondikeFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in KlondikeFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(KlondikeFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.False(string.IsNullOrWhiteSpace(script.KlondikeCard));
			Assert.DoesNotContain(script.KlondikeCard, script.Board);
			Assert.All(script.HoleCards.Values, cards => Assert.DoesNotContain(script.KlondikeCard, cards));

			for (var dealer = 0; dealer < KlondikeFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(KlondikeFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(KlondikeFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(KlondikeFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = KlondikeDeckScript.Build(dealer, holes, script.KlondikeCard!, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(script.KlondikeCard, deck[holes.Count * 2]);
			}

			var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, KlondikeFiveMaxCatalog.SeatCount).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void Orbit_AssignsEachSeatAsDealerSmallBlindAndBigBlindOnce()
	{
		var dealerCounts = new int[KlondikeFiveMaxCatalog.SeatCount];
		var smallBlindCounts = new int[KlondikeFiveMaxCatalog.SeatCount];
		var bigBlindCounts = new int[KlondikeFiveMaxCatalog.SeatCount];

		for (var hand = 0; hand < KlondikeFiveMaxCatalog.SeatCount; hand++)
		{
			var dealer = hand % KlondikeFiveMaxCatalog.SeatCount;
			dealerCounts[dealer]++;
			smallBlindCounts[HoldEmSeats.Index(KlondikeFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.SmallBlind)]++;
			bigBlindCounts[HoldEmSeats.Index(KlondikeFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.BigBlind)]++;
		}

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
		Assert.All(smallBlindCounts, count => Assert.Equal(1, count));
		Assert.All(bigBlindCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, KlondikeFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < KlondikeFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(KlondikeFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, KlondikeFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void ShowdownDescriptions_MatchTheWildCardEvaluator()
	{
		Assert.Equal(
			"Straight flush to the Ten",
			Describe(KlondikeFiveMaxCatalog.WildStraightFlush, HoldEmPosition.BigBlind));
		Assert.Equal(
			"Five of a kind, Kings",
			Describe(KlondikeFiveMaxCatalog.FiveOfAKind, HoldEmPosition.Button));
		Assert.Equal(
			"Three of a kind, Nines",
			Describe(KlondikeFiveMaxCatalog.OnlyTheKlondikeCardIsWild, HoldEmPosition.Button));
		Assert.Equal(
			"Pair of Kings",
			Describe(KlondikeFiveMaxCatalog.OnlyTheKlondikeCardIsWild, HoldEmPosition.BigBlind));

		Assert.Equal(
			KlondikeFiveMaxCatalog.WildStraightFlush.WinnerDescription,
			Describe(KlondikeFiveMaxCatalog.WildStraightFlush, KlondikeFiveMaxCatalog.WildStraightFlush.Winner));
		Assert.Equal(
			KlondikeFiveMaxCatalog.FiveOfAKind.WinnerDescription,
			Describe(KlondikeFiveMaxCatalog.FiveOfAKind, KlondikeFiveMaxCatalog.FiveOfAKind.Winner));
		Assert.Equal(
			KlondikeFiveMaxCatalog.OnlyTheKlondikeCardIsWild.WinnerDescription,
			Describe(
				KlondikeFiveMaxCatalog.OnlyTheKlondikeCardIsWild,
				KlondikeFiveMaxCatalog.OnlyTheKlondikeCardIsWild.Winner));
	}

	private static string Describe(HoldEmHandScript script, HoldEmPosition position)
	{
		var holes = string.Join(' ', script.HoleCards[position]);
		var community = $"{script.KlondikeCard} {string.Join(' ', script.Board)}".ToCards();
		var hand = new KlondikeHand(holes.ToCards(), community, community.First());
		return HandDescriptionFormatter.GetHandDescription(hand);
	}
}
