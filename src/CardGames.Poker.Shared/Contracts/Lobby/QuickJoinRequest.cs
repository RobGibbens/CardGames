using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Lobby;

public record QuickJoinRequest(
    PokerVariant? Variant = null,
    int? MinSmallBlind = null,
    int? MaxSmallBlind = null);
