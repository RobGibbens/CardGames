using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Lobby;

public record CreateTableRequest(
    string Name,
    PokerVariant Variant,
    int SmallBlind,
    int BigBlind,
    int MinBuyIn,
    int MaxBuyIn,
    int MaxSeats,
    TablePrivacy Privacy,
    string? Password = null);
