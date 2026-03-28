using Refit;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

[System.CodeDom.Compiler.GeneratedCode("Manual", "1.0.0.0")]
public partial interface IInBetweenApi
{
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/in-between/{gameId}/ace-choice")]
	Task<IApiResponse<InBetweenAceChoiceSuccessful>> InBetweenAceChoiceAsync(
		System.Guid gameId, [Body] InBetweenAceChoiceRequest body, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/in-between/{gameId}/place-bet")]
	Task<IApiResponse<InBetweenPlaceBetSuccessful>> InBetweenPlaceBetAsync(
		System.Guid gameId, [Body] InBetweenPlaceBetRequest body, CancellationToken cancellationToken = default);
}
