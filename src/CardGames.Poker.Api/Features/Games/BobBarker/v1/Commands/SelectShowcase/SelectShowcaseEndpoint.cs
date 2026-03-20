using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;

public static class SelectShowcaseEndpoint
{
    public static RouteGroupBuilder MapSelectShowcase(this RouteGroupBuilder group)
    {
        group.MapPost("{gameId:guid}/showcase",
                async (Guid gameId, SelectShowcaseRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var command = new SelectShowcaseCommand(gameId, request.ShowcaseCardIndex, request.PlayerSeatIndex);
                    var result = await mediator.Send(command, cancellationToken);

                    return result.Match(
                        success => Results.Ok(success),
                        error => error.Code switch
                        {
                            SelectShowcaseErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
                            SelectShowcaseErrorCode.NotInShowcasePhase => Results.Conflict(new { error.Message }),
                            SelectShowcaseErrorCode.NotPlayerTurn => Results.Conflict(new { error.Message }),
                            SelectShowcaseErrorCode.NoEligiblePlayers => Results.Conflict(new { error.Message }),
                            SelectShowcaseErrorCode.AlreadySelected => Results.Conflict(new { error.Message }),
                            SelectShowcaseErrorCode.InvalidCardIndex => Results.UnprocessableEntity(new { error.Message }),
                            SelectShowcaseErrorCode.InsufficientCards => Results.UnprocessableEntity(new { error.Message }),
                            _ => Results.Problem(error.Message)
                        });
                })
            .WithName("BobBarkerSelectShowcase")
            .WithSummary("Select Showcase Card")
            .WithDescription("Marks exactly one Bob Barker hole card as the player's showcase card. Pre-flop betting begins after all eligible players choose.")
            .Produces<SelectShowcaseSuccessful>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }
}