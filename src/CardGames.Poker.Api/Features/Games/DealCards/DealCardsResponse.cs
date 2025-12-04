namespace CardGames.Poker.Api.Features.Games.DealCards;

/// <summary>
/// Response returned after dealing cards to players.
/// </summary>
public record DealCardsResponse(
    bool Success,
    string Phase,
    List<PlayerCardCountResponse> PlayerCardCounts,
    Guid? CurrentPlayerToAct
);

/// <summary>
/// Represents the number of cards dealt to a player.
/// </summary>
public record PlayerCardCountResponse(
    Guid PlayerId,
    string PlayerName,
    int CardCount
);
