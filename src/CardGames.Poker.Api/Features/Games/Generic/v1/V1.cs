using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.Generic.v1;

/// <summary>
/// Defines v1 API endpoints for generic poker game operations.
/// </summary>
/// <remarks>
/// These generic endpoints work with any poker variant by routing game-specific
/// logic through <see cref="GameFlow.IGameFlowHandler"/> implementations.
/// </remarks>
public static class V1
{
    /// <summary>
    /// Maps v1 generic game endpoints.
    /// </summary>
    /// <param name="app">The versioned endpoint route builder.</param>
    public static void MapV1(this IVersionedEndpointRouteBuilder app)
    {
        var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/generic")
            .HasApiVersion(1.0)
            .WithTags([Feature.Name])
            .AddFluentValidationAutoValidation();

        mapGroup
            .MapStartHand()
            .MapPerformShowdown();
    }
}
