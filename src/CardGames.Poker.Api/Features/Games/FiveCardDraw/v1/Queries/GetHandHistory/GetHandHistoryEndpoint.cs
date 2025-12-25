using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetHandHistory;

/// <summary>
/// Endpoint for retrieving hand history.
/// </summary>
public static class GetHandHistoryEndpoint
{
    /// <summary>
    /// Maps the GetHandHistory endpoint.
    /// </summary>
    public static RouteGroupBuilder MapGetHandHistory(this RouteGroupBuilder group)
    {
        group.MapGet("{gameId:guid}/history",
                async (Guid gameId, IMediator mediator, int take = 25, int skip = 0, Guid? playerId = null, CancellationToken cancellationToken = default) =>
                {
                    var query = new GetHandHistoryQuery(gameId, playerId, take, skip);
                    var response = await mediator.Send(query, cancellationToken);
                    return Results.Ok(response);
                })
            .WithName(nameof(MapGetHandHistory).TrimPrefix("Map"))
            .WithSummary("Get hand history for a game")
            .WithDescription("Retrieves the hand history for a specific game, ordered newest-first. " +
                           "Optionally provide a playerId to get per-player result context.")
            .MapToApiVersion(1.0)
            .Produces<HandHistoryListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}
