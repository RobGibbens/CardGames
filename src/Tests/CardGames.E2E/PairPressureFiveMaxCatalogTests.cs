using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.E2E;

public class PairPressureFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in PairPressureFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(PairPressureFiveMaxCatalog.SeatCount, script.Holdings.Count);
			Assert.Equal(PairPressureFiveMaxCatalog.OpeningPot, script.OpeningPot);
			Assert.Equal(PairPressureDeckScript.BringIn(script), script.BringIn);

			foreach (var street in script.Actions.Select(action => action.Street).Distinct())
			{
				var first = script.Actions.First(action => action.Street == street);
				Assert.Equal(PairPressureDeckScript.FirstActor(script, street), first.Actor);
			}

			foreach (var action in script.Actions.Where(action => action.WildLabels is not null))
			{
				Assert.Equal(
					PairPressureDeckScript.WildLabels(script, action.Street),
					action.WildLabels);
			}

			for (var dealer = 0; dealer < PairPressureFiveMaxCatalog.SeatCount; dealer++)
			{
				var trace = PairPressureDeckScript.Trace(dealer, script);
				Assert.Equal(52, trace.Deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(PairPressureFiveMaxCatalog.SeatCount, trace.Seats.Count);
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new PairPressureLedger(Enumerable.Repeat(1_000, PairPressureFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < PairPressureFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(PairPressureFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, PairPressureFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[PairPressureFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < PairPressureFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % PairPressureFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void FoldToBringIn_AndBigBetRaise_HaveNoWildRanks()
	{
		Assert.Empty(PairPressureDeckScript.WildLabels(
			PairPressureFiveMaxCatalog.FoldToBringIn,
			PairPressureStreet.Third));
		Assert.Empty(PairPressureDeckScript.WildLabels(
			PairPressureFiveMaxCatalog.BigBetRaise,
			PairPressureStreet.Fifth));
	}

	[Fact]
	public void FirstPair_EightsStayWildWhenFourthStreetDoesNotPair()
	{
		Assert.Equal(
			[PairPressureFiveMaxCatalog.Eights],
			PairPressureDeckScript.WildLabels(PairPressureFiveMaxCatalog.FirstPair, PairPressureStreet.Third));
		Assert.Equal(
			[PairPressureFiveMaxCatalog.Eights],
			PairPressureDeckScript.WildLabels(PairPressureFiveMaxCatalog.FirstPair, PairPressureStreet.Fourth));
	}

	[Fact]
	public void EvictOldestPair_DropsEightsAndIgnoresTheLaterEight()
	{
		var script = PairPressureFiveMaxCatalog.EvictOldestPair;

		Assert.Equal(
			[PairPressureFiveMaxCatalog.Eights, PairPressureFiveMaxCatalog.Fives],
			PairPressureDeckScript.WildLabels(script, PairPressureStreet.Third));
		Assert.Equal(
			[PairPressureFiveMaxCatalog.Fives, PairPressureFiveMaxCatalog.Kings],
			PairPressureDeckScript.WildLabels(script, PairPressureStreet.Fourth));
		Assert.Equal(
			[PairPressureFiveMaxCatalog.Fives, PairPressureFiveMaxCatalog.Kings],
			PairPressureDeckScript.WildLabels(script, PairPressureStreet.Fifth));
		Assert.Contains("8s", PairPressureDeckScript.FaceUpWalk(script, PairPressureStreet.Fifth));
		Assert.Equal(HoldEmPosition.UnderTheGun, PairPressureDeckScript.FirstActor(script, PairPressureStreet.Fourth));
		Assert.Equal(HoldEmPosition.UnderTheGun, PairPressureDeckScript.FirstActor(script, PairPressureStreet.Fifth));
	}

	[Fact]
	public void WildShowdown_KingsReplaceEightsBeforeTheRiver()
	{
		var script = PairPressureFiveMaxCatalog.WildShowdown;

		Assert.Equal(
			[PairPressureFiveMaxCatalog.Eights, PairPressureFiveMaxCatalog.Fives],
			PairPressureDeckScript.WildLabels(script, PairPressureStreet.Fourth));
		Assert.Equal(
			[PairPressureFiveMaxCatalog.Fives, PairPressureFiveMaxCatalog.Kings],
			PairPressureDeckScript.WildLabels(script, PairPressureStreet.Sixth));
		Assert.True(script.ShowsWild);
	}

	[Fact]
	public void WildShowdown_BigBlindFourAcesBeatUnderTheGunFourQueens()
	{
		var script = PairPressureFiveMaxCatalog.WildShowdown;
		var faceUp = PairPressureDeckScript.FaceUpWalk(script).Select(PairPressureDeckScript.Parse).ToList();
		var winner = Hand(script.Holdings[HoldEmPosition.BigBlind], faceUp);
		var loser = Hand(script.Holdings[HoldEmPosition.UnderTheGun], faceUp);

		Assert.Equal(HandType.Quads, winner.Type);
		Assert.Equal(HandType.Quads, loser.Type);
		Assert.Contains(winner.WildCards, card => card.Symbol == Symbol.King);
		Assert.DoesNotContain(loser.WildCards, card => card.Symbol == Symbol.Eight);
		Assert.Contains(loser.WildCards, card => card.Symbol == Symbol.Five);
		Assert.Contains(loser.WildCards, card => card.Symbol == Symbol.King);
		Assert.True(winner.Strength > loser.Strength);
	}

	[Fact]
	public void WildShowdown_KeepingEightsWildWouldMakeFiveQueensWin()
	{
		var script = PairPressureFiveMaxCatalog.WildShowdown;
		var faceUp = PairPressureDeckScript.FaceUpWalk(script).Select(PairPressureDeckScript.Parse).ToList();
		var winner = Hand(script.Holdings[HoldEmPosition.BigBlind], faceUp);
		var loserHolding = script.Holdings[HoldEmPosition.UnderTheGun];
		var loserCards = Cards(loserHolding);
		var wildIfEightsStayed = loserCards
			.Where(card => card.Symbol is Symbol.Five or Symbol.King or Symbol.Eight)
			.ToList();

		var reversed = WildCardHandEvaluator.EvaluateBestHand(
			loserCards,
			wildIfEightsStayed,
			HandTypeStrengthRanking.Classic);

		Assert.Equal(HandType.FiveOfAKind, reversed.Type);
		Assert.True(reversed.Strength > winner.Strength);
	}

	private static PairPressureHand Hand(PairPressureHolding holding, IReadOnlyList<Card> faceUp) =>
		new(
			[PairPressureDeckScript.Parse(holding.FirstHole), PairPressureDeckScript.Parse(holding.SecondHole)],
			[
				PairPressureDeckScript.Parse(holding.Door),
				PairPressureDeckScript.Parse(holding.Fourth!),
				PairPressureDeckScript.Parse(holding.Fifth!),
				PairPressureDeckScript.Parse(holding.Sixth!)
			],
			PairPressureDeckScript.Parse(holding.Seventh!),
			faceUp);

	private static List<Card> Cards(PairPressureHolding holding) =>
	[
		PairPressureDeckScript.Parse(holding.FirstHole),
		PairPressureDeckScript.Parse(holding.SecondHole),
		PairPressureDeckScript.Parse(holding.Door),
		PairPressureDeckScript.Parse(holding.Fourth!),
		PairPressureDeckScript.Parse(holding.Fifth!),
		PairPressureDeckScript.Parse(holding.Sixth!),
		PairPressureDeckScript.Parse(holding.Seventh!)
	];
}
