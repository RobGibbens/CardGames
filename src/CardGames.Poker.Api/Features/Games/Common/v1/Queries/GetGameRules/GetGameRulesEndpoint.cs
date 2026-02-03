using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;

/// <summary>
/// Endpoint for retrieving game rules by game type code.
/// </summary>
public static class GetGameRulesEndpoint
{
    public static RouteGroupBuilder MapGetGameRules(this RouteGroupBuilder group)
    {
        group.MapGet("rules/{gameTypeCode}",
                async (string gameTypeCode, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var response = await mediator.Send(new GetGameRulesQuery(gameTypeCode), cancellationToken);
                    return response is null
                        ? Results.NotFound()
                        : Results.Ok(response);
                })
            .WithName($"{nameof(MapGetGameRules).TrimPrefix("Map")}")
            .WithSummary(nameof(MapGetGameRules).TrimPrefix("Map"))
            .WithDescription("Retrieve game rules metadata for a specific game type by its code.")
            .MapToApiVersion(1.0)
            .Produces<GetGameRulesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("{gameId:guid}/rules",
                async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var response = await mediator.Send(new GetGameRulesByGameIdQuery(gameId), cancellationToken);
                    return response is null
                        ? Results.NotFound()
                        : Results.Ok(response);
                })
            .WithName("GetGameRulesByGameId")
            .WithSummary("GetGameRulesByGameId")
            .WithDescription("Retrieve game rules metadata for a specific game by its identifier.")
            .MapToApiVersion(1.0)
            .Produces<GetGameRulesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}
