using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.BobBarker.v1;

public static class V1
{
    public static void MapV1(this IVersionedEndpointRouteBuilder app)
    {
        var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/bob-barker")
            .HasApiVersion(1.0)
            .WithTags(["Bob Barker"])
            .AddFluentValidationAutoValidation();

        mapGroup.MapSelectShowcase();
    }
}