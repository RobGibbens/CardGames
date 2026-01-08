using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetHandHistory;

/// <summary>
/// Query to retrieve hand history for a specific game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="CurrentUserPlayerId">The player ID of the current user (for per-player result context).</param>
/// <param name="Take">Maximum number of entries to return.</param>
/// <param name="Skip">Number of entries to skip (for pagination).</param>
public record GetHandHistoryQuery(
    Guid GameId,
    Guid? CurrentUserPlayerId = null,
    int Take = 25,
    int Skip = 0) : IRequest<HandHistoryListDto>;
