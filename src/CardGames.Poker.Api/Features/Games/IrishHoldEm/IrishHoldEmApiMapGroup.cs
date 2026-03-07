using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm;

[EndpointMapGroup]
public static class IrishHoldEmApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var irishHoldEm = app.NewVersionedApi("IrishHoldEm");
		irishHoldEm.MapV1();
	}
}
