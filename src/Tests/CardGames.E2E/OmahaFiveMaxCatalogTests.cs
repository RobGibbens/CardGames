using CardGames.Core.French.Cards.Extensions;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Api.Features.Games.Common.Showdown;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.E2E;

public class OmahaFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEachButton()
	{
		Assert.Equal(5, OmahaFiveMaxCatalog.Orbit.Count);

		foreach (var script in OmahaFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(OmahaFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);
			Assert.All(script.HoleCards.Values, cards => Assert.Equal(4, cards.Count));

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			for (var dealer = 0; dealer < OmahaFiveMaxCatalog.SeatCount; dealer++)
			{
				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(OmahaFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = OmahaDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, OmahaFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < OmahaFiveMaxCatalog.Orbit.Count; hand++)
			ledger.Apply(OmahaFiveMaxCatalog.Orbit[hand], dealerSeat: hand);

		Assert.Equal(5_000, ledger.StartingSum);
		Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
	}

	[Fact]
	public void BoardQuads_AreAFullHouseForTheButton_WhileHoldEmChopsTheBoard()
	{
		var script = OmahaFiveMaxCatalog.BoardQuadsAreAFullHouse;
		var button = OmahaHandOf(script, HoldEmPosition.Button);
		var bigBlind = OmahaHandOf(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.FullHouse, button.Type);
		Assert.Equal(HandType.Trips, bigBlind.Type);
		Assert.True(button.Strength > bigBlind.Strength);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));

		var holdemButton = HoldemHandOf(script, HoldEmPosition.Button);
		var holdemBigBlind = HoldemHandOf(script, HoldEmPosition.BigBlind);
		Assert.Equal(HandType.Quads, holdemButton.Type);
		Assert.Equal(HandType.Quads, holdemBigBlind.Type);
		Assert.Equal(holdemButton.Strength, holdemBigBlind.Strength);
	}

	[Fact]
	public void TwoHoleHearts_MakeTheFlush_ThatFourDiamondsCannotPlay()
	{
		var script = OmahaFiveMaxCatalog.TwoHoleCardsMakeTheFlush;
		var button = OmahaHandOf(script, HoldEmPosition.Button);
		var bigBlind = OmahaHandOf(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Flush, button.Type);
		Assert.Equal(HandType.HighCard, bigBlind.Type);
		Assert.True(button.Strength > bigBlind.Strength);
		Assert.Equal("Ace high flush", HandDescriptionFormatter.GetHandDescription(button));
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));
	}

	[Fact]
	public void SplitStraight_GivesTheOddChipToTheFirstWinnerClockwiseFromTheButton()
	{
		var script = OmahaFiveMaxCatalog.SplitStraight;
		var underTheGun = OmahaHandOf(script, HoldEmPosition.UnderTheGun);
		var button = OmahaHandOf(script, HoldEmPosition.Button);
		var bigBlind = OmahaHandOf(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.Straight, underTheGun.Type);
		Assert.Equal(underTheGun.Strength, button.Strength);
		Assert.True(underTheGun.Strength > bigBlind.Strength);
		Assert.Equal("Straight to the Jack", HandDescriptionFormatter.GetHandDescription(underTheGun));
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));

		const int dealer = 0;
		var underTheGunSeat = HoldEmSeats.Index(OmahaFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.UnderTheGun);
		var buttonSeat = HoldEmSeats.Index(OmahaFiveMaxCatalog.SeatCount, dealer, HoldEmPosition.Button);
		var ordered = OddChipOrder.OrderClockwiseFromDealer(
			[buttonSeat, underTheGunSeat],
			dealer,
			seat => seat);
		var shares = OddChipOrder.Distribute(35, ordered);

		Assert.Equal([underTheGunSeat, buttonSeat], ordered);
		Assert.Equal([18, 17], shares);
		Assert.Equal(HoldEmPosition.UnderTheGun, script.Winner);
		Assert.Equal(18, script.AwardedAmount);
	}

	private static OmahaHand OmahaHandOf(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static HoldemHand HoldemHandOf(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static IReadOnlyCollection<CardGames.Core.French.Cards.Card> Cards(IReadOnlyList<string> cards) =>
		string.Join(' ', cards).ToCards();
}
