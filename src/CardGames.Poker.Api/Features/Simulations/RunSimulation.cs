using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Common.Mapping;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Simulations.Holdem;
using FluentValidation;
using MediatR;

namespace CardGames.Poker.Api.Features.Simulations;

/// <summary>
/// Command to run a poker simulation.
/// </summary>
public record RunSimulationCommand(
    PokerVariant Variant,
    int NumberOfHands,
    IReadOnlyList<SimulationPlayerRequest> Players,
    IReadOnlyList<string>? FlopCards = null,
    string? TurnCard = null,
    string? RiverCard = null) : IRequest<IResult>;

/// <summary>
/// Validator for RunSimulationCommand.
/// </summary>
public sealed class RunSimulationCommandValidator : AbstractValidator<RunSimulationCommand>
{
    public RunSimulationCommandValidator()
    {
        RuleFor(x => x.Players)
            .NotEmpty()
            .Must(p => p.Count >= 2)
            .WithMessage("At least 2 players are required for a simulation");

        RuleFor(x => x.NumberOfHands)
            .InclusiveBetween(1, 100000)
            .WithMessage("Number of hands must be between 1 and 100,000");
    }
}

/// <summary>
/// Handler for RunSimulationCommand.
/// </summary>
public sealed class RunSimulationHandler : IRequestHandler<RunSimulationCommand, IResult>
{
    public Task<IResult> Handle(RunSimulationCommand request, CancellationToken cancellationToken)
    {
        var result = request.Variant switch
        {
            PokerVariant.TexasHoldem => RunHoldemSimulation(request),
            PokerVariant.Omaha => RunOmahaSimulation(),
            _ => Results.BadRequest($"Simulation not yet supported for variant: {request.Variant}")
        };

        return Task.FromResult(result);
    }

    private static IResult RunHoldemSimulation(RunSimulationCommand request)
    {
        var simulation = new HoldemSimulation();

        foreach (var player in request.Players)
        {
            var holeCards = player.HoleCards?
                .Select(c => c.ToCard())
                .ToList() ?? [];

            simulation.WithPlayer(new HoldemPlayer(player.Name, holeCards));
        }

        // Add flop if provided
        if (request.FlopCards != null && request.FlopCards.Count == 3)
        {
            var flopCards = request.FlopCards.Select(c => c.ToCard()).ToList();
            simulation.WithFlop(flopCards);

            // Add turn if provided
            if (!string.IsNullOrEmpty(request.TurnCard))
            {
                simulation.WithTurn(request.TurnCard.ToCard());
            }

            // Add river if provided (only if turn was also provided)
            if (!string.IsNullOrEmpty(request.RiverCard) && !string.IsNullOrEmpty(request.TurnCard))
            {
                simulation.WithRiver(request.RiverCard.ToCard());
            }
        }

        var result = simulation.SimulateWithFullDeck(request.NumberOfHands);

        var winDistributions = HandsEvaluation.GroupByWins(result.Hands);
        var handDistributions = HandsEvaluation.AllMadeHandDistributions(result.Hands);

        var playerResults = winDistributions.Select(wd => new PlayerSimulationResult(
            Name: wd.Name,
            Wins: wd.Wins,
            WinPercentage: (double)wd.Percentage * 100,
            Ties: wd.Ties,
            TiePercentage: (double)wd.TiePercentage * 100
        )).ToList();

        // Aggregate hand distributions across all players
        var aggregatedDistributions = handDistributions
            .SelectMany(kvp => kvp.Value)
            .GroupBy(td => td.Type)
            .Select(g => new HandTypeDistribution(
                HandType: ApiMapper.MapHandType(g.Key),
                Count: g.Sum(td => td.Occurrences),
                Percentage: (double)g.Sum(td => td.Occurrences) / (request.NumberOfHands * request.Players.Count) * 100
            ))
            .OrderByDescending(h => h.Count)
            .ToList();

        return Results.Ok(new SimulationResultResponse(
            Variant: PokerVariant.TexasHoldem,
            TotalHands: request.NumberOfHands,
            PlayerResults: playerResults,
            HandDistributions: aggregatedDistributions
        ));
    }

    private static IResult RunOmahaSimulation()
    {
        return Results.BadRequest("Omaha simulation is not yet implemented. Use Texas Hold'em for now.");
    }
}

/// <summary>
/// Endpoint for running simulations.
/// </summary>
public static class RunSimulationEndpoint
{
    public static IEndpointRouteBuilder MapRunSimulationEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/simulations/run", async (
            RunSimulationRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new RunSimulationCommand(
                request.Variant,
                request.NumberOfHands,
                request.Players,
                request.FlopCards,
                request.TurnCard,
                request.RiverCard);

            return await mediator.Send(command, cancellationToken);
        })
        .WithName("RunSimulation")
        .WithDescription("Run a poker simulation with the specified parameters")
        .WithTags("Simulations");

        return app;
    }
}
