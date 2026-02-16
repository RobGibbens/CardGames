using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Profile.v1.Commands.UploadAvatar;

namespace CardGames.Poker.Api.Features.Profile.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/profile")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		mapGroup.MapUploadAvatar();
	}
}
