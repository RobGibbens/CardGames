namespace CardGames.E2E.Infrastructure;

public enum ScrewYourNeighborDecision
{
	Keep,
	Trade
}

/// <summary>
///     One keep-or-trade decision. <see cref="CardShown" /> is the card that player sees before acting.
///     <see cref="TradeVisible" /> is false when that player holds a king or the player on their left does,
///     unless they are the dealer trading with the deck.
/// </summary>
public sealed record ScrewYourNeighborAction(
	int Seat,
	ScrewYourNeighborDecision Decision,
	string CardShown,
	bool TradeVisible);

/// <summary>
///     One Screw Your Neighbor hand at a five-seat table.
///     Dealt cards are keyed by seat. Deal order is left of the dealer, dealer last.
/// </summary>
public sealed class ScrewYourNeighborHandScript
{
	public required string Name { get; init; }

	public required int DealerSeat { get; init; }

	public required IReadOnlyDictionary<int, string> DealtCards { get; init; }

	/// <summary>
	///     The next undealt card, taken only when the dealer trades.
	/// </summary>
	public string? DealerDeckCard { get; init; }

	public required IReadOnlyList<int> SkippedSeats { get; init; }

	public required IReadOnlyList<ScrewYourNeighborAction> Actions { get; init; }

	public required IReadOnlyDictionary<int, string> FinalCards { get; init; }

	public required IReadOnlyList<int> Losers { get; init; }

	public required int PotAfter { get; init; }

	public required IReadOnlyList<int> StacksAfter { get; init; }

	public required int NextDealerSeat { get; init; }

	public bool DealerTrades =>
		Actions.Any(action => action.Seat == DealerSeat && action.Decision == ScrewYourNeighborDecision.Trade);

	public IReadOnlyList<string> DealOrderCards
	{
		get
		{
			var cards = new string[ScrewYourNeighborFiveMaxCatalog.SeatCount];
			for (var step = 1; step <= cards.Length; step++)
			{
				var seat = (DealerSeat + step) % cards.Length;
				cards[step - 1] = DealtCards[seat];
			}

			return cards;
		}
	}
}

/// <summary>
///     Four hands at a five-player Screw Your Neighbor table.
///     Each player buys in for three stacks of <see cref="StackSize" />. The same deck continues.
///     Ace of hearts is dealt on hand 1, so the dealer's deck trade on hand 3 takes the ace of spades.
/// </summary>
public static class ScrewYourNeighborFiveMaxCatalog
{
	public const int SeatCount = 5;
	public const int StackSize = 100;
	public const int BuyIn = StackSize * 3;
	public const int TableChips = BuyIn * SeatCount;
	public const string NextCard = "7d";

	public static ScrewYourNeighborHandScript EveryoneKeeps { get; } = new()
	{
		Name = "Everyone keeps",
		DealerSeat = 0,
		DealtCards = new Dictionary<int, string>
		{
			[1] = "7h",
			[2] = "Ah",
			[3] = "9h",
			[4] = "Th",
			[0] = "Jh"
		},
		SkippedSeats = [],
		Actions =
		[
			Act(1, ScrewYourNeighborDecision.Keep, "7h"),
			Act(2, ScrewYourNeighborDecision.Keep, "Ah"),
			Act(3, ScrewYourNeighborDecision.Keep, "9h"),
			Act(4, ScrewYourNeighborDecision.Keep, "Th"),
			Act(0, ScrewYourNeighborDecision.Keep, "Jh")
		],
		FinalCards = new Dictionary<int, string>
		{
			[0] = "Jh",
			[1] = "7h",
			[2] = "Ah",
			[3] = "9h",
			[4] = "Th"
		},
		Losers = [2],
		PotAfter = StackSize,
		StacksAfter = [BuyIn, BuyIn, BuyIn - StackSize, BuyIn, BuyIn],
		NextDealerSeat = 1
	};

	public static ScrewYourNeighborHandScript TradeWithNeighbor { get; } = new()
	{
		Name = "Trade with the neighbor",
		DealerSeat = 1,
		DealtCards = new Dictionary<int, string>
		{
			[2] = "2h",
			[3] = "Qh",
			[4] = "8h",
			[0] = "9d",
			[1] = "Td"
		},
		SkippedSeats = [],
		Actions =
		[
			Act(2, ScrewYourNeighborDecision.Trade, "2h"),
			Act(3, ScrewYourNeighborDecision.Keep, "2h"),
			Act(4, ScrewYourNeighborDecision.Keep, "8h"),
			Act(0, ScrewYourNeighborDecision.Keep, "9d"),
			Act(1, ScrewYourNeighborDecision.Keep, "Td")
		],
		FinalCards = new Dictionary<int, string>
		{
			[0] = "9d",
			[1] = "Td",
			[2] = "Qh",
			[3] = "2h",
			[4] = "8h"
		},
		Losers = [3],
		PotAfter = StackSize * 2,
		StacksAfter = [BuyIn, BuyIn, BuyIn - StackSize, BuyIn - StackSize, BuyIn],
		NextDealerSeat = 2
	};

	public static ScrewYourNeighborHandScript KingBlocksDealerTakesDeck { get; } = new()
	{
		Name = "King blocks and the dealer takes the deck",
		DealerSeat = 2,
		DealtCards = new Dictionary<int, string>
		{
			[3] = "6h",
			[4] = "Kh",
			[0] = "5h",
			[1] = "8d",
			[2] = "4h"
		},
		DealerDeckCard = "As",
		SkippedSeats = [4],
		Actions =
		[
			Act(3, ScrewYourNeighborDecision.Keep, "6h", tradeVisible: false),
			Act(0, ScrewYourNeighborDecision.Keep, "5h"),
			Act(1, ScrewYourNeighborDecision.Keep, "8d"),
			Act(2, ScrewYourNeighborDecision.Trade, "4h")
		],
		FinalCards = new Dictionary<int, string>
		{
			[0] = "5h",
			[1] = "8d",
			[2] = "As",
			[3] = "6h",
			[4] = "Kh"
		},
		Losers = [2],
		PotAfter = StackSize * 3,
		StacksAfter = [BuyIn, BuyIn, BuyIn - (StackSize * 2), BuyIn - StackSize, BuyIn],
		NextDealerSeat = 3
	};

	public static ScrewYourNeighborHandScript TiedAces { get; } = new()
	{
		Name = "Tied aces",
		DealerSeat = 3,
		DealtCards = new Dictionary<int, string>
		{
			[4] = "Ac",
			[0] = "Ad",
			[1] = "9c",
			[2] = "Kc",
			[3] = "Qc"
		},
		SkippedSeats = [1, 2],
		Actions =
		[
			Act(4, ScrewYourNeighborDecision.Keep, "Ac"),
			Act(0, ScrewYourNeighborDecision.Keep, "Ad"),
			Act(3, ScrewYourNeighborDecision.Keep, "Qc")
		],
		FinalCards = new Dictionary<int, string>
		{
			[0] = "Ad",
			[1] = "9c",
			[2] = "Kc",
			[3] = "Qc",
			[4] = "Ac"
		},
		Losers = [0, 4],
		PotAfter = StackSize * 5,
		StacksAfter = [BuyIn - StackSize, BuyIn, BuyIn - (StackSize * 2), BuyIn - StackSize, BuyIn - StackSize],
		NextDealerSeat = 4
	};

	public static IReadOnlyList<ScrewYourNeighborHandScript> Hands { get; } =
	[
		EveryoneKeeps,
		TradeWithNeighbor,
		KingBlocksDealerTakesDeck,
		TiedAces
	];

	private static ScrewYourNeighborAction Act(
		int seat,
		ScrewYourNeighborDecision decision,
		string cardShown,
		bool tradeVisible = true) =>
		new(seat, decision, cardShown, tradeVisible);
}
