using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.E2E;

public class FiveCardDrawFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEachButton()
	{
		foreach (var script in FiveCardDrawFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(FiveCardDrawFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(5, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards)
				         .Concat(FiveCardDrawFiveMaxCatalog.DrawStub(script)))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			foreach (var action in script.Actions.Where(action => action.Kind == FiveCardDrawActionKind.Discard))
			{
				Assert.NotNull(action.Discards);
				Assert.NotNull(action.Draws);
				Assert.Equal(action.Discards.Count, action.Draws.Count);
				Assert.InRange(action.Discards.Count, 1, 4);
				Assert.All(action.Discards, card => Assert.Contains(card, script.HoleCards[action.Actor]));
			}

			var stub = FiveCardDrawFiveMaxCatalog.DrawStub(script);
			for (var dealer = 0; dealer < FiveCardDrawFiveMaxCatalog.SeatCount; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(FiveCardDrawFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = FiveCardDrawDeckScript.Build(dealer, holes, stub);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());

				var dealOrder = holes.Keys
					.OrderBy(seat => (seat - dealer - 1 + FiveCardDrawFiveMaxCatalog.SeatCount) %
					                 FiveCardDrawFiveMaxCatalog.SeatCount)
					.ToList();
				var prefix = dealOrder.SelectMany(seat => holes[seat]).Concat(stub).ToArray();
				Assert.Equal(prefix, deck.Take(prefix.Length));
			}
		}
	}

	[Fact]
	public void Orbit_DrawsInServerOrder_AndKeepsFiveCardHands()
	{
		foreach (var script in FiveCardDrawFiveMaxCatalog.Orbit)
		{
			var actual = script.Actions
				.Where(action => action.Kind is FiveCardDrawActionKind.StandPat or FiveCardDrawActionKind.Discard)
				.Select(action => action.Actor)
				.ToArray();

			Assert.Equal(FiveCardDrawFiveMaxCatalog.DrawOrder(script), actual);

			foreach (var actor in actual)
				Assert.Equal(5, FiveCardDrawFiveMaxCatalog.FinalHand(script, actor).Count);
		}
	}

	[Fact]
	public void AceDiscard_ThrowsFourCards_AndKeepsTheAce()
	{
		var discard = FiveCardDrawFiveMaxCatalog.AceDiscardsFour.Actions.Single(action =>
			action.Kind == FiveCardDrawActionKind.Discard && action.Discards!.Count == 4);

		Assert.Equal(HoldEmPosition.UnderTheGun, discard.Actor);
		var discards = discard.Discards ?? throw new InvalidOperationException("The ace discard lists no cards.");
		Assert.Contains("Ah", FiveCardDrawFiveMaxCatalog.AceDiscardsFour.HoleCards[discard.Actor]);
		Assert.DoesNotContain("Ah", discards);
		Assert.Contains("Ah", FiveCardDrawFiveMaxCatalog.FinalHand(
			FiveCardDrawFiveMaxCatalog.AceDiscardsFour,
			discard.Actor));
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, FiveCardDrawFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < FiveCardDrawFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = FiveCardDrawFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, FiveCardDrawFiveMaxCatalog.Orbit.Count);
		Assert.Equal(50, FiveCardDrawFiveMaxCatalog.OpeningPot);
	}

	[Fact]
	public void ContestedShowdowns_AwardTheBestFiveCardHand()
	{
		foreach (var script in FiveCardDrawFiveMaxCatalog.Orbit.Where(script => !script.WonByFold))
		{
			var folded = script.Actions
				.Where(action => action.Kind == FiveCardDrawActionKind.Fold)
				.Select(action => action.Actor)
				.ToHashSet();
			var strengths = FiveCardDrawFiveMaxCatalog.ActionOrder
				.Where(position => !folded.Contains(position))
				.ToDictionary(
					position => position,
					position => new DrawHand(FiveCardDrawFiveMaxCatalog.FinalHand(script, position).Select(Parse).ToArray()).Strength);

			var best = strengths.MaxBy(pair => pair.Value);
			Assert.Equal(script.Winner, best.Key);
			Assert.Equal(1, strengths.Count(pair => pair.Value == best.Value));
		}
	}

	private static Card Parse(string code)
	{
		var symbol = char.ToUpperInvariant(code[0]) switch
		{
			'A' => Symbol.Ace,
			'K' => Symbol.King,
			'Q' => Symbol.Queen,
			'J' => Symbol.Jack,
			'T' => Symbol.Ten,
			'9' => Symbol.Nine,
			'8' => Symbol.Eight,
			'7' => Symbol.Seven,
			'6' => Symbol.Six,
			'5' => Symbol.Five,
			'4' => Symbol.Four,
			'3' => Symbol.Three,
			'2' => Symbol.Deuce,
			_ => throw new InvalidOperationException($"Unexpected rank in '{code}'.")
		};
		var suit = char.ToLowerInvariant(code[1]) switch
		{
			'h' => Suit.Hearts,
			'd' => Suit.Diamonds,
			's' => Suit.Spades,
			'c' => Suit.Clubs,
			_ => throw new InvalidOperationException($"Unexpected suit in '{code}'.")
		};
		return new Card(suit, symbol);
	}
}
