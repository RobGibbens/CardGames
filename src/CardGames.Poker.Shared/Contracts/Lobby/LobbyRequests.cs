using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Lobby;

public record TablesListRequest(
    PokerVariant? Variant = null,
    int? MinSmallBlind = null,
    int? MaxSmallBlind = null,
    int? MinAvailableSeats = null,
    bool? HideFullTables = null,
    bool? HideEmptyTables = null);
