using MediatR;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Query to retrieve the current player's turn state for a specific game.
/// </summary>
public record GetCurrentPlayerTurnQuery(Guid GameId) : IRequest<GetCurrentPlayerTurnResponse?>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetCurrentPlayerTurnQuery)}:{GameId}";
}
