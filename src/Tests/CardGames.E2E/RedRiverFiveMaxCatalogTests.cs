using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class RedRiverFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndDealsABonusOnlyAfterARedRiver()
	{
		foreach (var script in RedRiverFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(RedRiverFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.True(script.Board.Count is 5 or 6);

			var river = script.Board[4];
			var riverIsRed = river.EndsWith('h') || river.EndsWith('d');
			Assert.Equal(riverIsRed ? 6 : 5, script.Board.Count);

			for (var dealer = 0; dealer < RedRiverFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(RedRiverFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(RedRiverFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(RedRiverFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = RedRiverDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());

				var holeCount = holes.Values.Sum(cards => cards.Count);
				Assert.Equal(river, deck[holeCount + 4], StringComparer.Ordinal);
				if (riverIsRed)
					Assert.Equal(script.Board[5], deck[holeCount + 5], StringComparer.Ordinal);
			}

			var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, RedRiverFiveMaxCatalog.SeatCount).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, RedRiverFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < RedRiverFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(RedRiverFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(4, RedRiverFiveMaxCatalog.Orbit.Count);
		Assert.Contains(RedRiverFiveMaxCatalog.Orbit, script => script.Board[4].EndsWith('h'));
		Assert.Contains(RedRiverFiveMaxCatalog.Orbit, script => script.Board[4].EndsWith('d'));
		Assert.Contains(RedRiverFiveMaxCatalog.Orbit, script => script.Board[4].EndsWith('c') || script.Board[4].EndsWith('s'));
	}
}
