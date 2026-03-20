using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Testing.v1.Commands.SeedUsers;

namespace CardGames.Poker.Api.Features.Testing.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/testing")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		mapGroup.MapSeedDevelopmentUsers();
	}
}