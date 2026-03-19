using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Queries.GetGeneratedTableName;

/// <summary>
/// Endpoint for retrieving a generated table name suggestion.
/// </summary>
public static class GetGeneratedTableNameEndpoint
{
    /// <summary>
    /// Maps the GET endpoint for retrieving a generated table name.
    /// </summary>
    /// <param name="group">The route group builder.</param>
    /// <returns>The route group builder for chaining.</returns>
    public static RouteGroupBuilder MapGetGeneratedTableName(this RouteGroupBuilder group)
    {
        group.MapGet("table-name",
                async (string? gameType, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var response = await mediator.Send(new GetGeneratedTableNameQuery(gameType), cancellationToken);
                    return Results.Ok(response);
                })
            .WithName($"Generic{nameof(MapGetGeneratedTableName).TrimPrefix("Map")}")
            .WithSummary("Get Generated Table Name (Generic)")
            .WithDescription("Retrieves a generated table name suggestion for a new table. Currently returns a placeholder value.")
            .Produces<GetGeneratedTableNameResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}