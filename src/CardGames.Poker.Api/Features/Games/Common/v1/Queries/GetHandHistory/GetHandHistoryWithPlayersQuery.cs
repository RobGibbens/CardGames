using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;

/// <summary>
/// Query to retrieve hand history with per-player results for expandable display.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="Take">Maximum number of entries to return.</param>
/// <param name="Skip">Number of entries to skip (for pagination).</param>
public record GetHandHistoryWithPlayersQuery(
    Guid GameId,
    int Take = 25,
    int Skip = 0) : IRequest<HandHistoryWithPlayersListDto>;
