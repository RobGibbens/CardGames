using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class BaseballFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in BaseballFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(BaseballFiveMaxCatalog.SeatCount, script.Holdings.Count);
			Assert.Equal(BaseballFiveMaxCatalog.OpeningPot, script.OpeningPot);

			for (var dealer = 0; dealer < BaseballFiveMaxCatalog.SeatCount; dealer++)
			{
				var trace = BaseballDeckScript.Trace(dealer, script);
				Assert.Equal(52, trace.Deck.Distinct(StringComparer.Ordinal).Count());
				Assert.Equal(BaseballFiveMaxCatalog.SeatCount, trace.Seats.Count);
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new BaseballLedger(Enumerable.Repeat(1_000, BaseballFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < BaseballFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(BaseballFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.Stacks.Sum());
		Assert.Equal(5, BaseballFiveMaxCatalog.Orbit.Count);
	}

	[Fact]
	public void Orbit_GivesEverySeatTheButtonOnce()
	{
		var dealerCounts = new int[BaseballFiveMaxCatalog.SeatCount];
		for (var hand = 0; hand < BaseballFiveMaxCatalog.Orbit.Count; hand++)
			dealerCounts[hand % BaseballFiveMaxCatalog.SeatCount]++;

		Assert.All(dealerCounts, count => Assert.Equal(1, count));
	}

	[Fact]
	public void DeclineFour_DealsTheFourAndNoExtraCard()
	{
		var dealer = BaseballFiveMaxCatalog.Orbit.ToList().IndexOf(BaseballFiveMaxCatalog.DeclineFour);
		var trace = BaseballDeckScript.Trace(dealer, BaseballFiveMaxCatalog.DeclineFour);
		var smallBlind = trace.Seats[HoldEmPosition.SmallBlind];

		Assert.Equal(["7c", "8c", "4h"], smallBlind.Cards);
		Assert.Equal(["4h"], smallBlind.UpCards);

		var four = trace.Deck.ToList().IndexOf("4h");
		Assert.Equal("9c", trace.Deck[four + 1]);
	}

	[Fact]
	public void BuyFourth_TakesTheNextCardAndSkipsThatSeatOnFifthStreet()
	{
		var dealer = BaseballFiveMaxCatalog.Orbit.ToList().IndexOf(BaseballFiveMaxCatalog.BuyFourth);
		var trace = BaseballDeckScript.Trace(dealer, BaseballFiveMaxCatalog.BuyFourth);
		var buyer = trace.Seats[HoldEmPosition.UnderTheGun];
		var bringIn = trace.Seats[HoldEmPosition.BigBlind];

		Assert.Equal(["6s", "7s", "Ah", "4d", "8s"], buyer.Cards);
		Assert.Equal(["Ah", "4d"], buyer.UpCards);
		Assert.Equal(["2c", "Kh", "Kd"], bringIn.UpCards);

		var four = trace.Deck.ToList().IndexOf("4d");
		Assert.Equal("8s", trace.Deck[four + 1]);
	}

	[Fact]
	public void WildShowdown_GivesTheWinnerANineAndTheLoserNoWild()
	{
		var winner = BaseballFiveMaxCatalog.WildShowdown.Holdings[HoldEmPosition.BigBlind];
		var loser = BaseballFiveMaxCatalog.WildShowdown.Holdings[HoldEmPosition.UnderTheGun];

		Assert.Equal("9h", winner.Seventh);
		Assert.DoesNotContain('3', $"{loser.FirstHole}{loser.SecondHole}{loser.Door}{loser.Fourth}{loser.Fifth}{loser.Sixth}{loser.Seventh}");
		Assert.DoesNotContain('9', $"{loser.FirstHole}{loser.SecondHole}{loser.Door}{loser.Fourth}{loser.Fifth}{loser.Sixth}{loser.Seventh}");
		Assert.True(BaseballFiveMaxCatalog.WildShowdown.ShowsWild);
		Assert.False(BaseballFiveMaxCatalog.WildShowdown.WonByFold);
	}
}
