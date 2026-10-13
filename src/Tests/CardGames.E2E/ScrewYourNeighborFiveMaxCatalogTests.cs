using CardGames.E2E.Infrastructure;

namespace CardGames.E2E;

public class ScrewYourNeighborFiveMaxCatalogTests
{
	[Fact]
	public void Hands_FollowKeepTradeRules_AndConserveFifteenHundredChips()
	{
		var deck = ScrewYourNeighborDeckScript.Build(
			ScrewYourNeighborFiveMaxCatalog.Hands,
			ScrewYourNeighborFiveMaxCatalog.NextCard);
		Assert.Equal(52, deck.Count);
		Assert.Equal(52, deck.Distinct(StringComparer.Ordinal).Count());

		var stacks = Enumerable.Repeat(ScrewYourNeighborFiveMaxCatalog.BuyIn, ScrewYourNeighborFiveMaxCatalog.SeatCount)
			.ToArray();
		var pot = 0;
		var consumed = 0;
		var dealer = 0;

		Assert.Equal(4, ScrewYourNeighborFiveMaxCatalog.Hands.Count);

		foreach (var hand in ScrewYourNeighborFiveMaxCatalog.Hands)
		{
			Assert.Equal(dealer, hand.DealerSeat);
			var trace = Trace(hand);

			Assert.Equal(hand.DealOrderCards, deck.Skip(consumed).Take(hand.DealOrderCards.Count));
			Assert.Equal(trace.Actors, hand.Actions.Select(action => action.Seat));
			Assert.Equal(trace.Skipped, hand.SkippedSeats);
			for (var seat = 0; seat < ScrewYourNeighborFiveMaxCatalog.SeatCount; seat++)
				Assert.Equal(hand.FinalCards[seat], trace.FinalCards[seat]);

			Assert.Equal(trace.Losers, hand.Losers);

			consumed += hand.DealOrderCards.Count;
			if (hand.DealerTrades)
			{
				Assert.Equal(hand.DealerDeckCard, deck[consumed]);
				consumed++;
			}

			foreach (var loser in trace.Losers)
				stacks[loser] -= ScrewYourNeighborFiveMaxCatalog.StackSize;

			pot += trace.Losers.Count * ScrewYourNeighborFiveMaxCatalog.StackSize;
			Assert.Equal(stacks, hand.StacksAfter);
			Assert.Equal(pot, hand.PotAfter);
			Assert.Equal(ScrewYourNeighborFiveMaxCatalog.TableChips, stacks.Sum() + pot);

			dealer = (dealer + 1) % ScrewYourNeighborFiveMaxCatalog.SeatCount;
			Assert.Equal(dealer, hand.NextDealerSeat);
		}

		Assert.Equal(ScrewYourNeighborFiveMaxCatalog.NextCard, deck[consumed]);
		Assert.Equal([200, 300, 100, 200, 200], stacks);
		Assert.Equal(500, pot);
	}

	[Fact]
	public void KingBlockHand_UsesTheRemainingAceBecauseHeartsWasAlreadyDealt()
	{
		var dealt = ScrewYourNeighborFiveMaxCatalog.Hands
			.SelectMany(hand => hand.DealtCards.Values)
			.ToHashSet(StringComparer.Ordinal);

		Assert.Contains("Ah", dealt);
		Assert.DoesNotContain("As", dealt);
		Assert.Equal("As", ScrewYourNeighborFiveMaxCatalog.KingBlocksDealerTakesDeck.DealerDeckCard);
		Assert.Equal("As", ScrewYourNeighborFiveMaxCatalog.KingBlocksDealerTakesDeck.FinalCards[2]);
	}

	/// <summary>
	///     Applies keep, trade, king skips, and ace-low ranking independently of the stored expectations.
	/// </summary>
	private static HandTrace Trace(ScrewYourNeighborHandScript hand)
	{
		var seatCount = ScrewYourNeighborFiveMaxCatalog.SeatCount;
		var cards = hand.DealtCards.ToDictionary(pair => pair.Key, pair => pair.Value);
		var actors = new List<int>();
		var skipped = new List<int>();
		var firstSeat = (hand.DealerSeat + 1) % seatCount;

		for (var step = 0; step < seatCount; step++)
		{
			var seat = (firstSeat + step) % seatCount;
			var ownsKing = IsKing(cards[seat]);
			var neighbor = (seat + 1) % seatCount;
			var neighborKing = IsKing(cards[neighbor]);
			var isDealer = seat == hand.DealerSeat;
			var isSkipped = seat != firstSeat && (ownsKing || (!isDealer && neighborKing));
			if (isSkipped)
			{
				skipped.Add(seat);
				continue;
			}

			var action = hand.Actions.Single(candidate => candidate.Seat == seat);
			var tradeVisible = !ownsKing && (isDealer || !neighborKing);
			Assert.Equal(cards[seat], action.CardShown);
			Assert.Equal(tradeVisible, action.TradeVisible);
			actors.Add(seat);

			if (action.Decision != ScrewYourNeighborDecision.Trade)
				continue;

			if (isDealer)
			{
				cards[seat] = hand.DealerDeckCard
				              ?? throw new InvalidOperationException($"Hand '{hand.Name}' dealer traded without a deck card.");
			}
			else
			{
				(cards[seat], cards[neighbor]) = (cards[neighbor], cards[seat]);
			}
		}

		var lowest = cards.Values.Min(Value);
		var losers = cards
			.Where(pair => Value(pair.Value) == lowest)
			.Select(pair => pair.Key)
			.OrderBy(seat => seat)
			.ToArray();

		return new HandTrace(actors, skipped, cards, losers);
	}

	private static bool IsKing(string card) => card[0] == 'K';

	private static int Value(string card) =>
		card[0] switch
		{
			'A' => 1,
			'T' => 10,
			'J' => 11,
			'Q' => 12,
			'K' => 13,
			_ => card[0] - '0'
		};

	private sealed record HandTrace(
		IReadOnlyList<int> Actors,
		IReadOnlyList<int> Skipped,
		IReadOnlyDictionary<int, string> FinalCards,
		IReadOnlyList<int> Losers);
}
