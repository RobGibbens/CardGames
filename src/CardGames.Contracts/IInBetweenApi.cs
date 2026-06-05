using System.CodeDom.Compiler;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

[GeneratedCode("Manual", "1.0.0.0")]
public interface IInBetweenApi
{
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/in-between/{gameId}/ace-choice")]
	Task<IApiResponse<InBetweenAceChoiceSuccessful>> InBetweenAceChoiceAsync(
		Guid gameId, [Body] InBetweenAceChoiceRequest body, CancellationToken cancellationToken = default);

	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/in-between/{gameId}/place-bet")]
	Task<IApiResponse<InBetweenPlaceBetSuccessful>> InBetweenPlaceBetAsync(
		Guid gameId, [Body] InBetweenPlaceBetRequest body, CancellationToken cancellationToken = default);
}