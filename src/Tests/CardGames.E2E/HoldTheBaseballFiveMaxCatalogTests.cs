using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.E2E.Infrastructure;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;

namespace CardGames.E2E;

public class HoldTheBaseballFiveMaxCatalogTests
{
	[Fact]
	public void EveryScript_ConservesChips_AndBuildsADeckFromEveryButton()
	{
		foreach (var script in HoldTheBaseballFiveMaxCatalog.Orbit)
		{
			Assert.Equal(0, script.Nets.Values.Sum());
			Assert.Equal(HoldTheBaseballFiveMaxCatalog.SeatCount, script.HoleCards.Count);
			Assert.DoesNotContain(HoldEmPosition.Middle, script.HoleCards.Keys);
			Assert.Equal(5, script.Board.Count);

			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var card in script.HoleCards.Values.SelectMany(cards => cards).Concat(script.Board))
				Assert.True(used.Add(card), $"Card '{card}' is repeated in '{script.Name}'.");

			for (var dealer = 0; dealer < HoldTheBaseballFiveMaxCatalog.SeatCount; dealer++)
			{
				var seats = script.HoleCards.Keys
					.Select(position => HoldEmSeats.Index(HoldTheBaseballFiveMaxCatalog.SeatCount, dealer, position))
					.ToArray();
				Assert.Equal(HoldTheBaseballFiveMaxCatalog.SeatCount, seats.Distinct().Count());

				var holes = script.HoleCards.ToDictionary(
					pair => HoldEmSeats.Index(HoldTheBaseballFiveMaxCatalog.SeatCount, dealer, pair.Key),
					pair => pair.Value);
				var deck = HoldEmDeckScript.Build(dealer, holes, script.Board);
				Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());
			}

			var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldTheBaseballFiveMaxCatalog.SeatCount).ToArray());
			ledger.Apply(script, dealerSeat: 0);
			Assert.Equal(ledger.StartingSum, ledger.Stacks.Sum());
		}
	}

	[Fact]
	public void Orbit_ConservesTheFiveThousandStartingChips()
	{
		var ledger = new HoldEmLedger(Enumerable.Repeat(1_000, HoldTheBaseballFiveMaxCatalog.SeatCount).ToArray());
		for (var hand = 0; hand < HoldTheBaseballFiveMaxCatalog.Orbit.Count; hand++)
		{
			var script = HoldTheBaseballFiveMaxCatalog.Orbit[hand];
			ledger.Apply(script.Name, script.Nets, dealerSeat: hand);
		}

		Assert.Equal(5, HoldTheBaseballFiveMaxCatalog.Orbit.Count);
		Assert.Equal(5_000, ledger.Stacks.Sum());
	}

	[Fact]
	public void WildHoleCheckdown_WildThreeBeatsTheAceHighThatWinsInHoldEm()
	{
		var script = HoldTheBaseballFiveMaxCatalog.WildHoleCheckdown;
		var button = Baseball(script, HoldEmPosition.Button);
		var bigBlind = Baseball(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.OnePair, button.Type);
		Assert.True(button.Strength > bigBlind.Strength);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));

		var holdEmButton = HoldEm(script, HoldEmPosition.Button);
		var holdEmBigBlind = HoldEm(script, HoldEmPosition.BigBlind);
		Assert.True(holdEmBigBlind.Strength > holdEmButton.Strength);
	}

	[Fact]
	public void RiverRaise_CommunityNineMakesFiveAces()
	{
		var script = HoldTheBaseballFiveMaxCatalog.RiverRaise;
		var button = Baseball(script, HoldEmPosition.Button);
		var bigBlind = Baseball(script, HoldEmPosition.BigBlind);

		Assert.Equal(HandType.FiveOfAKind, button.Type);
		Assert.True(button.Strength > bigBlind.Strength);
		Assert.Equal("Five of a kind, Aces", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(button));
	}

	[Fact]
	public void CommunityWildSplit_BothShowdownHandsAreTheSameRoyalFlush()
	{
		var script = HoldTheBaseballFiveMaxCatalog.CommunityWildSplit;
		var bigBlind = Baseball(script, HoldEmPosition.BigBlind);
		var underTheGun = Baseball(script, HoldEmPosition.UnderTheGun);

		Assert.Equal(HandType.StraightFlush, bigBlind.Type);
		Assert.Equal(bigBlind.Strength, underTheGun.Strength);
		Assert.Equal("Royal flush", script.WinnerDescription);
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(bigBlind));
		Assert.Equal(script.WinnerDescription, HandDescriptionFormatter.GetHandDescription(underTheGun));
	}

	private static HoldTheBaseballHand Baseball(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static HoldemHand HoldEm(HoldEmHandScript script, HoldEmPosition position) =>
		new(Cards(script.HoleCards[position]), Cards(script.Board));

	private static IReadOnlyCollection<Card> Cards(IReadOnlyList<string> cards) =>
		string.Join(' ', cards).ToCards();
}
