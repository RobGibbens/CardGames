using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class CrazyPineappleFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEachButton()
	{
		foreach (var script in CrazyPineappleFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(CrazyPineappleFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(3, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			foreach (var action in script.Actions.Where(action => action.Kind == CrazyPineappleActionKind.Discard))
			{
				Assert.Contains(action.Card, script.HoleCards[action.Actor]);
			}

			for (var dealer = 0; dealer < CrazyPineappleFiveMaxCatalog.Orbit.Count; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(CrazyPineappleFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = CrazyPineappleDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}
		}
	}

	[Fact]
	public void Orbit_DiscardsInServerOrder_FromEachHandsButton()
	{
		for (var hand = 0; hand < CrazyPineappleFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = CrazyPineappleFiveMaxCatalog.Orbit[hand];
			var actual = script.Actions
				.Where(action => action.Kind == CrazyPineappleActionKind.Discard)
				.Select(action => action.Actor)
				.ToArray();

			Assert.Equal(CrazyPineappleFiveMaxCatalog.DiscardOrder(script, dealerSeat: hand), actual);
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, CrazyPineappleFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < CrazyPineappleFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = CrazyPineappleFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(4, CrazyPineappleFiveMaxCatalog.Orbit.Count);
	}
}
