using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.SetSittingOut;

/// <summary>
/// Endpoint for updating sitting out status.
/// </summary>
public static class SetSittingOutEndpoint
{
    /// <summary>
    /// Maps the POST endpoint for updating sitting out status.
    /// </summary>
    public static RouteGroupBuilder MapSetSittingOut(this RouteGroupBuilder group)
    {
        group.MapPost("{gameId:guid}/sit-out",
                async (Guid gameId, SetSittingOutRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var command = new SetSittingOutCommand(gameId, request.IsSittingOut);
                    var result = await mediator.Send(command, cancellationToken);
                    
                    return result.Match(
                        _ => Results.Ok(),
                        error => Results.BadRequest(new { Message = error })
                    );
                })
            .WithName($"{nameof(MapSetSittingOut).TrimPrefix("Map")}")
            .WithSummary("Set Sitting Out Status")
            .WithDescription("Updates the player's sitting out status.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}

