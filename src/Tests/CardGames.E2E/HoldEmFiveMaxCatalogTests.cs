using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class HoldEmFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in HoldEmFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(HoldEmFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);

			for (var dealer = 0; dealer < HoldEmFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(HoldEmFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(HoldEmFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(HoldEmFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = HoldEmDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}

			var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldEmFiveMaxCatalog.SeatCount).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void Orbit_AssignsEachSeatAsDealerSmallBlindAndBigBlindOnce()
	{
		var dealerCounts = new int[HoldEmFiveMaxCatalog.SeatCount];
		var smallBlindCounts = new int[HoldEmFiveMaxCatalog.SeatCount];
		var bigBlindCounts = new int[HoldEmFiveMaxCatalog.SeatCount];

		for (var hand = 0; hand < HoldEmFiveMaxCatalog.SeatCount; hand++)
		{
			var dealer = hand % HoldEmFiveMaxCatalog.SeatCount;
			dealerCounts[dealer]++;
			smallBlindCounts[HoldEmSeats.Index(HoldEmFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.SmallBlind)]++;
			bigBlindCounts[HoldEmSeats.Index(HoldEmFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.BigBlind)]++;
		}

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
		Assert.All(smallBlindCounts, count => Assert.Equal(1, count));
		Assert.All(bigBlindCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldEmFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < HoldEmFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(HoldEmFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, HoldEmFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void SplitPot_OddChipGoesToTheBigBlind_FromEveryButton()
	{
		for (var dealer = 0; dealer < HoldEmFiveMaxCatalog.SeatCount; dealer++)
		{
			var bigBlind = HoldEmSeats.Index(HoldEmFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.BigBlind);
			var underTheGun = HoldEmSeats.Index(HoldEmFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.UnderTheGun);
			var firstPaid = new[] { bigBlind, underTheGun }
				.OrderBy(seat => seat > dealer ? 0 : 1)
				.ThenBy(seat => seat)
				.First();

			Assert.Equal(bigBlind, firstPaid);
		}
	}

	[Fact]
	public void Cutoff_IsTheLastSeat()
	{
		Assert.Equal(4, HoldEmSeats.Offset(HoldEmFiveMaxCatalog.SeatCount, HoldEmPosition.Cutoff));
		Assert.Equal(5, HoldEmSeats.Offset(HoldEmSeats.Count, HoldEmPosition.Cutoff));
	}
}
