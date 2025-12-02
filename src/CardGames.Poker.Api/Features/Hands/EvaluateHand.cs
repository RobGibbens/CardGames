using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Common.Mapping;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using FluentValidation;
using MediatR;

namespace CardGames.Poker.Api.Features.Hands;

/// <summary>
/// Command to evaluate a poker hand.
/// </summary>
public record EvaluateHandCommand(IReadOnlyList<string> Cards) : IRequest<IResult>;

/// <summary>
/// Validator for EvaluateHandCommand.
/// </summary>
public sealed class EvaluateHandCommandValidator : AbstractValidator<EvaluateHandCommand>
{
    public EvaluateHandCommandValidator()
    {
        RuleFor(x => x.Cards)
            .NotEmpty()
            .Must(c => c.Count == 5)
            .WithMessage("Hand must contain exactly 5 cards");
    }
}

/// <summary>
/// Handler for EvaluateHandCommand.
/// </summary>
public sealed class EvaluateHandHandler : IRequestHandler<EvaluateHandCommand, IResult>
{
    public Task<IResult> Handle(EvaluateHandCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var cards = request.Cards.Select(c => c.ToCard()).ToList();
            var hand = new DrawHand(cards);
            var description = HandDescriptionFormatter.GetHandDescription(hand);

            var result = Results.Ok(new EvaluateHandResponse(
                Cards: ApiMapper.ToCardDtos(cards),
                HandType: ApiMapper.MapHandType(hand.Type),
                Description: description,
                Strength: hand.Strength
            ));

            return Task.FromResult(result);
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(Results.BadRequest($"Invalid card format: {ex.Message}"));
        }
    }
}

/// <summary>
/// Endpoint for evaluating hands.
/// </summary>
public static class EvaluateHandEndpoint
{
    public static IEndpointRouteBuilder MapEvaluateHandEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/hands/evaluate", async (
            EvaluateHandRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new EvaluateHandCommand(request.Cards);
            return await mediator.Send(command, cancellationToken);
        })
        .WithName("EvaluateHand")
        .WithDescription("Evaluate a 5-card poker hand")
        .WithTags("Hands");

        return app;
    }
}
