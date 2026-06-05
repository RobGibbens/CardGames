using System.CodeDom.Compiler;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

/// <summary>
///     Minimal Refit interface for Screw Your Neighbor API endpoints.
///     This will be replaced by auto-generated Refit output once the full OpenAPI spec is updated.
/// </summary>
[GeneratedCode("Manual", "1.0.0.0")]
public interface IScrewYourNeighborApi
{
	/// <summary>KeepOrTrade</summary>
	/// <remarks>Record a player's keep or trade decision in Screw Your Neighbor.</remarks>
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/screw-your-neighbor/{gameId}/keep-or-trade")]
	Task<IApiResponse<KeepOrTradeSuccessful>> ScrewYourNeighborKeepOrTradeAsync(Guid gameId,
		[Body] KeepOrTradeRequest body, CancellationToken cancellationToken = default);
}