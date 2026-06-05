using System.CodeDom.Compiler;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

/// <summary>
///     Minimal Refit interface for Texas Hold'Em API endpoints.
///     This will be replaced by auto-generated Refit output once the full OpenAPI spec is updated.
/// </summary>
[GeneratedCode("Manual", "1.0.0.0")]
public interface IHoldEmApi
{
	/// <summary>Start Hand</summary>
	/// <remarks>Starts a new Hold'Em hand — shuffles deck, posts blinds, and deals hole cards.</remarks>
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/hold-em/{gameId}/start")]
	Task<IApiResponse<StartHandSuccessful>> HoldEmStartHandAsync(Guid gameId,
		CancellationToken cancellationToken = default);

	/// <summary>Process Betting Action</summary>
	/// <remarks>Processes a betting action from the current player in a Hold'Em game.</remarks>
	[Headers("Accept: application/json, application/problem+json", "Content-Type: application/json")]
	[Post("/api/v1/games/hold-em/{gameId}/betting/actions")]
	Task<IApiResponse<ProcessBettingActionSuccessful>> HoldEmProcessBettingActionAsync(Guid gameId,
		[Body] ProcessBettingActionRequest body, CancellationToken cancellationToken = default);

	/// <summary>Perform Showdown</summary>
	/// <remarks>Evaluates remaining players' hands and awards the pot(s) to the winner(s).</remarks>
	[Headers("Accept: application/json, application/problem+json")]
	[Post("/api/v1/games/hold-em/{gameId}/showdown")]
	Task<IApiResponse<PerformShowdownSuccessful>> HoldEmPerformShowdownAsync(Guid gameId,
		CancellationToken cancellationToken = default);
}