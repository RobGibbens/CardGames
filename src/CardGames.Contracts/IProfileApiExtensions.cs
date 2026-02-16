using Refit;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

public partial interface IProfileApi
{
	[Headers("Accept: application/json, application/problem+json")]
	[Multipart]
	[Post("/api/v1/profile/avatar")]
	Task<IApiResponse<UploadAvatarResponse>> UploadAvatarAsync([AliasAs("file")] StreamPart file, CancellationToken cancellationToken = default);
}
