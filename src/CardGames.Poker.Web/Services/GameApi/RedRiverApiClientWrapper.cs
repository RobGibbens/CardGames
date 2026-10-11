using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

/// <summary>
///     Red River reuses the Texas Hold'em endpoint family. Server-side game code
///     applies the conditional bonus community card and best-five evaluation.
/// </summary>
public sealed class RedRiverApiClientWrapper(IHoldEmApi client) : IGameApiClient
{
	public string GameTypeCode => "REDRIVER";

	public async Task<bool> StartGameAsync(Guid gameId)
	{
		var startResponse = await client.HoldEmStartHandAsync(gameId);
		return startResponse.IsSuccessStatusCode;
	}

	public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId,
		ProcessBettingActionRequest request)
	{
		return await client.HoldEmProcessBettingActionAsync(gameId, request);
	}

	public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
	{
		return Task.FromResult(new ProcessDrawResult
		{
			IsSuccess = false,
			ErrorMessage = "Draw phase not supported for Red River."
		});
	}

	public Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
	{
		return client.HoldEmPerformShowdownAsync(gameId);
	}

	public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
	{
		return Task.FromResult(false);
	}
}
