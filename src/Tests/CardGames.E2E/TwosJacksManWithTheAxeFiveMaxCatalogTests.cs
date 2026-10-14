using CardGames.Core.French.Cards;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.E2E;

public class TwosJacksManWithTheAxeFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEachButton()
	{
		foreach (var script in TwosJacksManWithTheAxeFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(TwosJacksManWithTheAxeFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(5, cards.Count));
			Assert.Equal(script.Pot, Contributions(script).Values.Sum());

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards)
				         .Concat(TwosJacksManWithTheAxeFiveMaxCatalog.DrawStub(script)))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			foreach (var action in script.Actions.Where(action =>
				         action.Kind == TwosJacksManWithTheAxeActionKind.Discard))
			{
				Assert.NotNull(action.Discards);
				Assert.NotNull(action.Draws);
				Assert.Equal(action.Discards!.Count, action.Draws!.Count);
				Assert.InRange(action.Discards.Count, 1, 4);
				Assert.All(action.Discards, card => Assert.Contains(card, script.HoleCards[action.Actor]));
				if (action.Discards.Count == 4)
					Assert.Contains("Ah", script.HoleCards[action.Actor]);
				else
					Assert.True(action.Discards.Count <= 3);
			}

			var stub = TwosJacksManWithTheAxeFiveMaxCatalog.DrawStub(script);
			for (var dealer = 0; dealer < TwosJacksManWithTheAxeFiveMaxCatalog.SeatCount; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(TwosJacksManWithTheAxeFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = FiveCardDrawDeckScript.Build(dealer, holes, stub);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());

				var dealOrder = holes.Keys
					.OrderBy(seat => (seat - dealer - 1 + TwosJacksManWithTheAxeFiveMaxCatalog.SeatCount) %
					                 TwosJacksManWithTheAxeFiveMaxCatalog.SeatCount)
					.ToList();
				var prefix = dealOrder.SelectMany(seat => holes[seat]).Concat(stub).ToArray();
				Assert.Equal(prefix, deck.Take(prefix.Length));
			}
		}
	}

	[Fact]
	public void Orbit_DrawsInServerOrder_AndKeepsFiveCardHands()
	{
		foreach (var script in TwosJacksManWithTheAxeFiveMaxCatalog.Orbit)
		{
			var actual = script.Actions
				.Where(action => action.Kind is TwosJacksManWithTheAxeActionKind.StandPat
					or TwosJacksManWithTheAxeActionKind.Discard)
				.Select(action => action.Actor)
				.ToArray();

			Assert.Equal(TwosJacksManWithTheAxeFiveMaxCatalog.DrawOrder(script), actual);

			foreach (var actor in actual)
				Assert.Equal(5, TwosJacksManWithTheAxeFiveMaxCatalog.FinalHand(script, actor).Count);
		}
	}

	[Fact]
	public void AceDiscard_ThrowsFourCards_AndKeepsTheAce()
	{
		var discard = TwosJacksManWithTheAxeFiveMaxCatalog.AceDiscardsFour.Actions.Single(action =>
			action.Kind == TwosJacksManWithTheAxeActionKind.Discard && action.Discards!.Count == 4);

		Assert.Equal(HoldEmPosition.UnderTheGun, discard.Actor);
		var discards = discard.Discards ?? throw new InvalidOperationException("The ace discard lists no cards.");
		Assert.Contains(
			"Ah",
			TwosJacksManWithTheAxeFiveMaxCatalog.AceDiscardsFour.HoleCards[discard.Actor]);
		Assert.DoesNotContain("Ah", discards);
		Assert.Contains(
			"Ah",
			TwosJacksManWithTheAxeFiveMaxCatalog.FinalHand(
				TwosJacksManWithTheAxeFiveMaxCatalog.AceDiscardsFour,
				discard.Actor));
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, TwosJacksManWithTheAxeFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < TwosJacksManWithTheAxeFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = TwosJacksManWithTheAxeFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, TwosJacksManWithTheAxeFiveMaxCatalog.Orbit.Count);
		Assert.Equal(50, TwosJacksManWithTheAxeFiveMaxCatalog.OpeningPot);
	}

	[Fact]
	public void ContestedShowdowns_AwardTheBestWildHand_AndPaySevensByTheRule()
	{
		AssertOutcome(
			TwosJacksManWithTheAxeFiveMaxCatalog.AxeOnTheDraw,
			HandType.FiveOfAKind,
			sevensRolledOver: true);
		AssertOutcome(
			TwosJacksManWithTheAxeFiveMaxCatalog.FoldBeforeDraw,
			handType: null,
			sevensRolledOver: false);
		AssertOutcome(
			TwosJacksManWithTheAxeFiveMaxCatalog.SevensSplitThePot,
			HandType.FiveOfAKind,
			sevensRolledOver: false);
		AssertOutcome(
			TwosJacksManWithTheAxeFiveMaxCatalog.AceDiscardsFour,
			HandType.Quads,
			sevensRolledOver: false);
		AssertOutcome(
			TwosJacksManWithTheAxeFiveMaxCatalog.BetRaiseAndSplit,
			HandType.FiveOfAKind,
			sevensRolledOver: false);
	}

	private static void AssertOutcome(
		TwosJacksManWithTheAxeHandScript script,
		HandType? handType,
		bool sevensRolledOver)
	{
		var contributions = Contributions(script);
		var payouts = Payouts(script);
		foreach (var position in TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder)
			Assert.Equal(payouts[position] - contributions[position], script.Nets[position]);

		if (script.WonByFold)
		{
			Assert.Null(script.SevensWinner);
			Assert.Equal(0, script.SevensAward);
			Assert.Equal(script.Pot, script.AwardedAmount);
			Assert.True(Evaluate(script, script.Winner).HasNaturalPairOfSevens());
			return;
		}

		var folded = script.Actions
			.Where(action => action.Kind == TwosJacksManWithTheAxeActionKind.Fold)
			.Select(action => action.Actor)
			.ToHashSet();
		var live = TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder
			.Where(position => !folded.Contains(position))
			.ToArray();
		var hands = live.ToDictionary(position => position, position => Evaluate(script, position));
		var best = hands.MaxBy(pair => pair.Value.Strength);
		Assert.Equal(script.Winner, best.Key);
		Assert.Equal(1, hands.Count(pair => pair.Value.Strength == best.Value.Strength));
		Assert.Equal(handType, best.Value.Type);

		var sevens = hands.Where(pair => pair.Value.HasNaturalPairOfSevens()).Select(pair => pair.Key).ToArray();
		if (sevensRolledOver)
		{
			Assert.Empty(sevens);
			Assert.Null(script.SevensWinner);
			Assert.Equal(0, script.SevensAward);
			Assert.Equal(script.Pot, script.AwardedAmount);
			return;
		}

		Assert.Equal(script.SevensWinner, Assert.Single(sevens));
		Assert.Equal(script.Pot / 2, script.SevensAward);
		if (script.SevensWinner == script.Winner)
			Assert.Equal(script.Pot, script.AwardedAmount);
		else
			Assert.Equal(script.Pot / 2, script.AwardedAmount);
	}

	private static Dictionary<HoldEmPosition, int> Payouts(TwosJacksManWithTheAxeHandScript script)
	{
		var payouts = TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder.ToDictionary(position => position, _ => 0);
		payouts[script.Winner] = script.AwardedAmount;
		if (script.SevensWinner is { } sevens && sevens != script.Winner)
			payouts[sevens] = script.SevensAward;

		return payouts;
	}

	private static Dictionary<HoldEmPosition, int> Contributions(TwosJacksManWithTheAxeHandScript script)
	{
		var contribution = TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder.ToDictionary(
			position => position,
			_ => TwosJacksManWithTheAxeFiveMaxCatalog.Ante);
		var street = TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder.ToDictionary(position => position, _ => 0);
		var drawing = false;
		foreach (var action in script.Actions)
		{
			if (action.Kind is TwosJacksManWithTheAxeActionKind.StandPat or TwosJacksManWithTheAxeActionKind.Discard)
			{
				if (!drawing)
				{
					street = TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder.ToDictionary(position => position, _ => 0);
					drawing = true;
				}

				continue;
			}

			if (drawing)
			{
				street = TwosJacksManWithTheAxeFiveMaxCatalog.ActionOrder.ToDictionary(position => position, _ => 0);
				drawing = false;
			}

			switch (action.Kind)
			{
				case TwosJacksManWithTheAxeActionKind.Bet:
					contribution[action.Actor] += action.Amount
						?? throw new InvalidOperationException("A bet needs an amount.");
					street[action.Actor] = action.Amount.Value;
					break;
				case TwosJacksManWithTheAxeActionKind.RaiseTo:
					var raiseTo = action.Amount ?? throw new InvalidOperationException("A raise needs an amount.");
					contribution[action.Actor] += raiseTo - street[action.Actor];
					street[action.Actor] = raiseTo;
					break;
				case TwosJacksManWithTheAxeActionKind.Call:
					contribution[action.Actor] += action.CallAmount
						?? throw new InvalidOperationException("A call needs an amount.");
					street[action.Actor] = action.StreetBet
						?? street[action.Actor] + action.CallAmount.Value;
					break;
			}
		}

		return contribution;
	}

	private static TwosJacksManWithTheAxeDrawHand Evaluate(
		TwosJacksManWithTheAxeHandScript script,
		HoldEmPosition actor) =>
		new(TwosJacksManWithTheAxeFiveMaxCatalog.FinalHand(script, actor).Select(Parse).ToArray());

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
