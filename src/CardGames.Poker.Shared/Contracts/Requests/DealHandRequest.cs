using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Requests;

/// <summary>
/// Request to deal a poker hand.
/// </summary>
public record DealHandRequest(
    PokerVariant Variant,
    int NumberOfPlayers,
    IReadOnlyList<string>? PlayerNames = null);
