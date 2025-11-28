using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Simulations.Holdem;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using CardGames.Poker.Shared.Enums;
using ApiMappingExtensions = CardGames.Poker.Api.Extensions.MappingExtensions;

namespace CardGames.Poker.Api.Features.Simulations;

/// <summary>
/// Module for simulation-related endpoints.
/// </summary>
public static class SimulationsModule
{
    public static IEndpointRouteBuilder MapSimulationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/simulations")
            .WithTags("Simulations")
            .WithOpenApi();

        group.MapPost("/run", RunSimulation)
            .WithName("RunSimulation")
            .WithDescription("Run a poker simulation with the specified parameters");

        return app;
    }

    private static IResult RunSimulation(RunSimulationRequest request)
    {
        if (request.Players.Count < 2)
        {
            return Results.BadRequest("At least 2 players are required for a simulation");
        }

        if (request.NumberOfHands < 1 || request.NumberOfHands > 100000)
        {
            return Results.BadRequest("Number of hands must be between 1 and 100,000");
        }

        return request.Variant switch
        {
            PokerVariant.TexasHoldem => RunHoldemSimulation(request),
            PokerVariant.Omaha => RunOmahaSimulation(request),
            _ => Results.BadRequest($"Simulation not yet supported for variant: {request.Variant}")
        };
    }

    private static IResult RunHoldemSimulation(RunSimulationRequest request)
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
                HandType: ApiMappingExtensions.MapHandType(g.Key),
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

    private static IResult RunOmahaSimulation(RunSimulationRequest request)
    {
        // For Omaha, we'd need to implement a similar simulation class
        // For now, return a not implemented response
        return Results.BadRequest("Omaha simulation is not yet implemented. Use Texas Hold'em for now.");
    }
}
