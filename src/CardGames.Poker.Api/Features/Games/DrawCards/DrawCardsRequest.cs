namespace CardGames.Poker.Api.Features.Games.DrawCards;

/// <summary>
/// Request to draw cards (discard and replace) in the draw phase.
/// </summary>
public record DrawCardsRequest(
	Guid PlayerId,
	List<int> DiscardIndices
);
