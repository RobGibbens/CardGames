using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class IrishHoldEmFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEachButton()
	{
		foreach (var script in IrishHoldEmFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(IrishHoldEmFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(4, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			foreach (var action in script.Actions.Where(action => action.Kind == IrishHoldEmActionKind.Discard))
			{
				Assert.Contains(action.Card, script.HoleCards[action.Actor]);
				Assert.Contains(action.SecondCard, script.HoleCards[action.Actor]);
				Assert.NotEqual(action.Card, action.SecondCard);
			}

			for (var dealer = 0; dealer < IrishHoldEmFiveMaxCatalog.Orbit.Count; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(IrishHoldEmFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = IrishHoldEmDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}
		}
	}

	[Fact]
	public void Orbit_DiscardsInServerOrder_FromEachHandsButton()
	{
		for (var hand = 0; hand < IrishHoldEmFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = IrishHoldEmFiveMaxCatalog.Orbit[hand];
			var actual = script.Actions
				.Where(action => action.Kind == IrishHoldEmActionKind.Discard)
				.Select(action => action.Actor)
				.ToArray();

			Assert.Equal(IrishHoldEmFiveMaxCatalog.DiscardOrder(script, dealerSeat: hand), actual);
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, IrishHoldEmFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < IrishHoldEmFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = IrishHoldEmFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(4, IrishHoldEmFiveMaxCatalog.Orbit.Count);
	}
}
