using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

public static class GetRabbitHuntEndpoint
{
    public static RouteGroupBuilder MapGetRabbitHunt(this RouteGroupBuilder group)
    {
        group.MapGet("{gameId:guid}/rabbit-hunt",
                async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var result = await mediator.Send(new GetRabbitHuntQuery(gameId), cancellationToken);

                    return result.Match<IResult>(
                        success => Results.Ok(success),
                        error => error.Code switch
                        {
                            GetRabbitHuntErrorCode.NotAuthenticated => Results.Unauthorized(),
                            GetRabbitHuntErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
                            GetRabbitHuntErrorCode.NotSeated => Results.Forbid(),
                            GetRabbitHuntErrorCode.UnsupportedGameType => Results.Conflict(new { error.Message }),
                            GetRabbitHuntErrorCode.RabbitHuntNotAvailable => Results.Conflict(new { error.Message }),
                            _ => Results.BadRequest(new { error.Message })
                        });
                })
            .WithName($"{nameof(MapGetRabbitHunt).TrimPrefix("Map")}")
            .WithSummary("GetRabbitHunt")
            .WithDescription("Returns the exact remaining community cards that would have been revealed if the hand had continued to a full board. The response is private to the requesting player and does not affect game outcome.")
            .MapToApiVersion(1.0)
            .Produces<GetRabbitHuntSuccessful>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }
}