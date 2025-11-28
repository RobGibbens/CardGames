using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Contracts.Responses;

/// <summary>
/// Response containing dealt hand information.
/// </summary>
public record DealHandResponse(
    PokerVariant Variant,
    IReadOnlyList<PlayerDto> Players,
    IReadOnlyList<CardDto>? CommunityCards,
    IReadOnlyList<string> Winners,
    string WinningHandDescription);
