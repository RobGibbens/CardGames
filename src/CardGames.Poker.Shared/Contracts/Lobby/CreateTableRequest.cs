using CardGames.Poker.Shared.DTOs;
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
    string? Password = null,
    LimitType LimitType = LimitType.NoLimit,
    int Ante = 0)
{
    /// <summary>
    /// Creates a TableConfigDto from the request parameters.
    /// </summary>
    public TableConfigDto ToConfig() => new(
        Variant: Variant,
        MaxSeats: MaxSeats,
        SmallBlind: SmallBlind,
        BigBlind: BigBlind,
        LimitType: LimitType,
        MinBuyIn: MinBuyIn,
        MaxBuyIn: MaxBuyIn,
        Ante: Ante);
}
