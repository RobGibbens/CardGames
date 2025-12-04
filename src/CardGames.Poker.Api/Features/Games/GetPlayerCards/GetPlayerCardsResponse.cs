namespace CardGames.Poker.Api.Features.Games.GetPlayerCards;

/// <summary>
/// Response containing a player's cards.
/// </summary>
public record GetPlayerCardsResponse(
    Guid PlayerId,
    List<string> Cards,
    int CardCount
);
