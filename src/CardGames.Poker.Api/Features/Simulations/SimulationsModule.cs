namespace CardGames.Poker.Api.Features.Simulations;

/// <summary>
/// Module for simulation-related endpoints using vertical slice architecture.
/// </summary>
public static class SimulationsModule
{
    public static IEndpointRouteBuilder MapSimulationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapRunSimulationEndpoint();
        return app;
    }
}
