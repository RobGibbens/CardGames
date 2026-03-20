using CardGames.Poker.Api.Features.Games.BobBarker.v1;

namespace CardGames.Poker.Api.Features.Games.BobBarker;

[EndpointMapGroup]
public static class BobBarkerApiMapGroup
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var bobBarker = app.NewVersionedApi("BobBarker");
        bobBarker.MapV1();
    }
}