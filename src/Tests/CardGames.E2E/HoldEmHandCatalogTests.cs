using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class HoldEmHandCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in HoldEmHandCatalog.All)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(HoldEmSeats.Count, script.HoleCards.Count);
			Assert.Equal(5, script.Board.Count);

			for (var dealer = 0; dealer < HoldEmSeats.Count; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(dealer, pair.Key),
					pair => pair.Value);
				try
				{
					var deck = HoldEmDeckScript.Build(dealer, holes, script.Board);
					Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
				}
				catch (ArgumentException exception)
				{
					throw new InvalidOperationException(
						$"Script '{script.Name}' cannot be dealt with the button on seat {dealer}.",
						exception);
				}
			}

			var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldEmSeats.Count).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void ThreeOrbits_AssignsEachSeatAsDealerSmallBlindAndBigBlindThreeTimes()
	{
		var dealerCounts = new int[HoldEmSeats.Count];
		var smallBlindCounts = new int[HoldEmSeats.Count];
		var bigBlindCounts = new int[HoldEmSeats.Count];
		var hands = HoldEmSeats.Count * 3;

		for (var hand = 0; hand < hands; hand++)
		{
			var dealer = hand % HoldEmSeats.Count;
			dealerCounts[dealer]++;
			smallBlindCounts[HoldEmSeats.Index(dealer, HoldEmPosition.SmallBlind)]++;
			bigBlindCounts[HoldEmSeats.Index(dealer, HoldEmPosition.BigBlind)]++;
		}

		Assert.All(dealerCounts, count => Assert.Equal(3, count));
		Assert.All(smallBlindCounts, count => Assert.Equal(3, count));
		Assert.All(bigBlindCounts, count => Assert.Equal(3, count));
	}

	[Fact]
	public void SplitPot_OddChipGoesToTheBigBlind_FromEveryButton()
	{
		for (var dealer = 0; dealer < HoldEmSeats.Count; dealer++)
		{
			var bigBlind = HoldEmSeats.Index(dealer, HoldEmPosition.BigBlind);
			var underTheGun = HoldEmSeats.Index(dealer, HoldEmPosition.UnderTheGun);
			var firstPaid = new[] { bigBlind, underTheGun }
				.OrderBy(seat => seat > dealer ? 0 : 1)
				.ThenBy(seat => seat)
				.First();

			Assert.Equal(bigBlind, firstPaid);
		}
	}

	[Fact]
	public void SidePot_NetsMatchTheFixedShortStacks()
	{
		var starting = new[] { 1_000, 1_000, 200, 80, 1_000, 1_000 };
		var ledger = new HoldEmLedger(starting);
		ledger.Apply(HoldEmHandCatalog.SidePot, dealerSeat: 0);

		Assert.Equal([1_000, 995, 70, 245, 970, 1_000], ledger.Stacks);
	}

	[Fact]
	public void ThreeTiers_NetsMatchTheFourContributionLevels()
	{
		var ledger = new HoldEmLedger([1_000, 40, 1_000, 80, 1_000, 1_000]);
		ledger.Apply(HoldEmHandCatalog.ThreeTiers, dealerSeat: 0);

		Assert.Equal([1_000, 160, 1_020, 120, 820, 1_000], ledger.Stacks);
		Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
	}

	[Fact]
	public void FoldedChipsRemain_PaysTheRaiserTheBlinds()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldEmSeats.Count).ToArray());
		ledger.Apply(HoldEmHandCatalog.FoldedChipsRemain, dealerSeat: 0);

		Assert.Equal(1_015, ledger.Stacks[HoldEmSeats.Index(0, HoldEmPosition.UnderTheGun)]);
		Assert.Equal(995, ledger.Stacks[HoldEmSeats.Index(0, HoldEmPosition.SmallBlind)]);
		Assert.Equal(990, ledger.Stacks[HoldEmSeats.Index(0, HoldEmPosition.BigBlind)]);
		Assert.Equal(6_000, ledger.Stacks.Sum());
	}

	[Fact]
	public void SplitMainDifferentSideWinner_NetsMatchTheChopAndTheSidePot()
	{
		var ledger = new HoldEmLedger([1_000, 50, 1_000, 50, 1_000, 1_000]);
		ledger.Apply(HoldEmHandCatalog.SplitMainDifferentSideWinner, dealerSeat: 0);

		Assert.Equal([1_000, 100, 1_050, 100, 850, 1_000], ledger.Stacks);
	}

	[Fact]
	public void UncalledRefund_ReturnsTheUnmatchedShoveAndAwardsTheMatchedPot()
	{
		var ledger = new HoldEmLedger([1_000, 1_000, 100, 1_000, 1_000, 1_000]);
		ledger.Apply(HoldEmHandCatalog.UncalledRefund, dealerSeat: 0);

		Assert.Equal([1_000, 995, 205, 900, 1_000, 1_000], ledger.Stacks);
		Assert.Equal(205, HoldEmHandCatalog.UncalledRefund.AwardedAmount);
	}

	[Fact]
	public void ThreeOrbits_ConserveTheSixThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldEmSeats.Count).ToArray());
		var scripts = HoldEmHandCatalog.Orbit;
		for (var hand = 0; hand < HoldEmSeats.Count * 3; hand++)
			ledger.Apply(scripts[hand % scripts.Count], hand % HoldEmSeats.Count);

		Assert.Equal(6_000, ledger.Stacks.Sum());
		Assert.Equal(6, scripts.Count);
	}
}
