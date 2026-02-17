using CardGames.Poker.Api.Features.Profile.v1;

namespace CardGames.Poker.Api.Features.Profile;

[EndpointMapGroup]
public static class ProfileApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var profile = app.NewVersionedApi("Profile");
		profile.MapV1();
	}
}
