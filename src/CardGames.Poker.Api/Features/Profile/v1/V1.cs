using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetAvatar;
using CardGames.Poker.Api.Features.Profile.v1.Commands.UploadAvatar;
using CardGames.Poker.Api.Features.Profile.v1.Commands.AddAccountChips;
using CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateGamePreferences;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierLedger;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetCashierSummary;
using CardGames.Poker.Api.Features.Profile.v1.Queries.GetGamePreferences;

namespace CardGames.Poker.Api.Features.Profile.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/profile")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		mapGroup.MapUploadAvatar();
		mapGroup.MapGetAvatar();
		mapGroup.MapGetCashierSummary();
		mapGroup.MapGetCashierLedger();
		mapGroup.MapAddAccountChips();
		mapGroup.MapGetGamePreferences();
		mapGroup.MapUpdateGamePreferences();
	}
}
