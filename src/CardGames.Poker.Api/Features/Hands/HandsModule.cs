namespace CardGames.Poker.Api.Features.Hands;

/// <summary>
/// Module for hand-related endpoints using vertical slice architecture.
/// </summary>
public static class HandsModule
{
    public static IEndpointRouteBuilder MapHandsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDealHandEndpoint();
        app.MapEvaluateHandEndpoint();
        return app;
    }
}
