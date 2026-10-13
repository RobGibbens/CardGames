using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.E2E;

public class KingsAndLowsFiveMaxCatalogTests
{
	[Fact]
	public void Orbit_HasFourHands_AndConservesChipsIntoTheCarriedPot()
	{
		Assert.Equal(4, KingsAndLowsFiveMaxCatalog.Orbit.Count);

		var stacks = Enumerable.Repeat(1_000, KingsAndLowsFiveMaxCatalog.SeatCount).ToArray();
		var carried = 0;
		var ledger = new KingsAndLowsLedger(stacks);
		for (var hand = 0; hand < KingsAndLowsFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = KingsAndLowsFiveMaxCatalog.Orbit[hand];
			var expectedOpening = carried == 0
				? KingsAndLowsFiveMaxCatalog.FirstPot
				: carried;
			Assert.Equal(expectedOpening, script.OpeningPot);
			Assert.All(stacks, stack => Assert.True(stack >= script.OpeningPot));
			Assert.Equal(carried - script.NextPot, script.Nets.Values.Sum());

			ledger.Apply(script, dealerSeat: hand);
			carried = script.NextPot;
			stacks = ledger.Stacks.ToArray();
		}

		Assert.Equal(5_000, ledger.StartingSum);
		Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum() + ledger.CarriedPot);
		Assert.Equal(800, ledger.CarriedPot);
	}

	[Fact]
	public void EveryScript_BuildsARoundRobinDeck_FromEachButton()
	{
		foreach (var script in KingsAndLowsFiveMaxCatalog.Orbit)
		{
			Assert.Equal(KingsAndLowsFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.Equal(KingsAndLowsFiveMaxCatalog.SeatCount, script.Nets.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);

			var decisions = script.Actions
				.Where(action => action.Kind is KingsAndLowsActionKind.Drop or KingsAndLowsActionKind.Stay)
				.Select(action => action.Actor)
				.ToArray();
			Assert.Equal(KingsAndLowsFiveMaxCatalog.ActionOrder, decisions);

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards)
				         .Concat(KingsAndLowsFiveMaxCatalog.DrawStub(script)))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			foreach (var action in script.Actions.Where(action => action.Kind == KingsAndLowsActionKind.Discard))
			{
				Assert.Equal(action.Discards!.Count, action.Draws!.Count);
				Assert.All(action.Discards, card => Assert.Contains(card, script.HoleCards[action.Actor]));
			}

			for (var dealer = 0; dealer < KingsAndLowsFiveMaxCatalog.SeatCount; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(KingsAndLowsFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var stub = KingsAndLowsFiveMaxCatalog.DrawStub(script);
				var roundRobin = KingsAndLowsDeckScript.Build(holes, stub, dealer, roundRobinBySeat: true);
				var fiveAtATime = KingsAndLowsDeckScript.Build(holes, stub, dealer);
				Assert.Equal(52, roundRobin.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(52, fiveAtATime.Distinct(StringComparer.Ordinal).Count());
				AssertRoundRobin(holes, roundRobin);
				AssertFiveAtATime(holes, fiveAtATime, dealer);
			}
		}
	}

	[Fact]
	public void Orbit_DrawsInClockwiseOrder_SkippingPlayersWhoDropped()
	{
		foreach (var script in KingsAndLowsFiveMaxCatalog.Orbit)
		{
			var actual = script.Actions
				.Where(action => action.Kind is KingsAndLowsActionKind.StandPat or KingsAndLowsActionKind.Discard)
				.Select(action => action.Actor)
				.ToArray();
			Assert.Equal(KingsAndLowsFiveMaxCatalog.DrawOrder(script), actual);
		}

		var deckHand = KingsAndLowsFiveMaxCatalog.DeckWins;
		var keeper = Assert.Single(deckHand.Actions, action => action.Kind == KingsAndLowsActionKind.DeckKeepAll);
		Assert.Equal(KingsAndLowsFiveMaxCatalog.DeckDecisionMaker(deckHand), keeper.Actor);
		Assert.NotEqual(HoldEmPosition.SmallBlind, keeper.Actor);
	}

	[Fact]
	public void ShowdownHands_RankTheScriptedWinner_UnderKingsAndLowsWilds()
	{
		foreach (var script in KingsAndLowsFiveMaxCatalog.Orbit.Where(script =>
			         script.Showdown == KingsAndLowsShowdownKind.Winner))
		{
			var winner = Hand(KingsAndLowsFiveMaxCatalog.FinalHand(script, script.Winner!.Value));
			foreach (var rival in KingsAndLowsFiveMaxCatalog.Stayers(script).Where(seat => seat != script.Winner))
			{
				var rivalHand = Hand(KingsAndLowsFiveMaxCatalog.FinalHand(script, rival));
				Assert.True(
					winner.Strength > rivalHand.Strength,
					$"'{script.Name}' expected {script.Winner} to beat {rival}.");
			}
		}

		var deck = Hand(KingsAndLowsFiveMaxCatalog.DeckWins.DeckCards!);
		var stayer = Hand(KingsAndLowsFiveMaxCatalog.FinalHand(
			KingsAndLowsFiveMaxCatalog.DeckWins,
			HoldEmPosition.SmallBlind));
		Assert.True(deck.Strength > stayer.Strength);
	}

	[Fact]
	public void AceLowStraight_WinsOnlyWhenTheAceIsTheWildCard()
	{
		var script = KingsAndLowsFiveMaxCatalog.AceLowStraight;
		var winnerCards = Cards(KingsAndLowsFiveMaxCatalog.FinalHand(script, HoldEmPosition.SmallBlind));
		var rivalCards = Cards(KingsAndLowsFiveMaxCatalog.FinalHand(script, HoldEmPosition.BigBlind));
		var winner = new KingsAndLowsDrawHand(winnerCards);
		var rival = new KingsAndLowsDrawHand(rivalCards);

		Assert.True(winner.Strength > rival.Strength);
		Assert.Contains(winner.WildCards, card => card.Symbol == Symbol.Ace);
		Assert.DoesNotContain(winner.WildCards, card => card.Symbol == Symbol.Five);

		var aceHighWilds = new WildCardRules(false).DetermineWildCards(winnerCards);
		Assert.Contains(aceHighWilds, card => card.Symbol == Symbol.Five);
		Assert.DoesNotContain(aceHighWilds, card => card.Symbol == Symbol.Ace);
		var aceHigh = WildCardHandEvaluator.EvaluateBestHand(
			winnerCards,
			aceHighWilds,
			HandTypeStrengthRanking.Classic);
		Assert.True(aceHigh.Strength < rival.Strength);
	}

	private static void AssertFiveAtATime(
		IReadOnlyDictionary<int, IReadOnlyList<string>> holes,
		IReadOnlyList<string> deck,
		int dealerSeat)
	{
		var totalSeats = holes.Keys.Max() + 1;
		var dealOrder = holes.Keys
			.OrderBy(seat => (seat - dealerSeat - 1 + totalSeats) % totalSeats)
			.ToArray();
		for (var player = 0; player < dealOrder.Length; player++)
		{
			for (var card = 0; card < 5; card++)
			{
				Assert.Equal(
					holes[dealOrder[player]][card],
					deck[(player * 5) + card]);
			}
		}
	}

	private static void AssertRoundRobin(
		IReadOnlyDictionary<int, IReadOnlyList<string>> holes,
		IReadOnlyList<string> deck)
	{
		var seats = holes.Keys.OrderBy(seat => seat).ToArray();
		for (var round = 0; round < 5; round++)
		{
			for (var index = 0; index < seats.Length; index++)
			{
				Assert.Equal(
					holes[seats[index]][round],
					deck[(round * seats.Length) + index]);
			}
		}
	}

	private static KingsAndLowsDrawHand Hand(IReadOnlyList<string> cards) =>
		new(Cards(cards));

	private static IReadOnlyCollection<Card> Cards(IReadOnlyList<string> cards) =>
		string.Join(' ', cards).ToCards();
}
