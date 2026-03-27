using Refit;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

public partial interface IProfileApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Multipart]
	[Post("/api/v1/profile/avatar")]
	Task<IApiResponse<UploadAvatarResponse>> UploadAvatarAsync([AliasAs("file")] StreamPart file, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/profile/cashier/summary")]
	Task<IApiResponse<CashierSummaryDto>> GetCashierSummaryAsync(CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/profile/cashier/add-chips")]
	Task<IApiResponse<AddAccountChipsResponse>> AddAccountChipsAsync([Body] AddAccountChipsRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/profile/cashier/ledger")]
   Task<IApiResponse<CashierLedgerPageDto>> GetCashierLedgerPageAsync([AliasAs("pageSize")][Query] int? pageSize = 10, [AliasAs("pageNumber")][Query] int? pageNumber = 1, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/profile/game-preferences")]
	Task<IApiResponse<GamePreferencesDto>> GetGamePreferencesAsync(CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Put("/api/v1/profile/game-preferences")]
	Task<IApiResponse<GamePreferencesDto>> UpdateGamePreferencesAsync([Body] UpdateGamePreferencesRequest request, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json")]
	[Get("/api/v1/profile/favorite-variants")]
	Task<IApiResponse<FavoriteVariantsDto>> GetFavoriteVariantsAsync(CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Put("/api/v1/profile/favorite-variants")]
	Task<IApiResponse<FavoriteVariantsDto>> UpdateFavoriteVariantsAsync([Body] UpdateFavoriteVariantsRequest request, CancellationToken cancellationToken = default);
}
