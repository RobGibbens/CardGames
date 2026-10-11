using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

/// <summary>
///     Razz reuses the Seven Card Stud endpoint family for streets and betting.
///     Showdown uses the generic handler so the pot is awarded to the best ace-to-five low.
/// </summary>
public sealed class RazzApiClientWrapper(ISevenCardStudApi client, IGamesApi gamesApi) : IGameApiClient
{
	public string GameTypeCode => "RAZZ";

	public async Task<bool> StartGameAsync(Guid gameId)
	{
		var startHandResponse = await client.SevenCardStudStartHandAsync(gameId);
		if (!startHandResponse.IsSuccessStatusCode) return false;

		var collectAntesResponse = await client.SevenCardStudCollectAntesAsync(gameId);
		if (!collectAntesResponse.IsSuccessStatusCode) return false;

		var dealHandsResponse = await client.SevenCardStudDealHandsAsync(gameId);
		return dealHandsResponse.IsSuccessStatusCode;
	}

	public Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId,
		ProcessBettingActionRequest request)
	{
		return client.SevenCardStudProcessBettingActionAsync(gameId, request);
	}

	public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
	{
		return Task.FromResult(new ProcessDrawResult
		{
			IsSuccess = false,
			ErrorMessage = "Draw phase not supported for Razz."
		});
	}

	public Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
	{
		return gamesApi.GenericPerformShowdownAsync(gameId);
	}

	public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
	{
		return Task.FromResult(false);
	}
}