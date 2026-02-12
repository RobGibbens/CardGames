using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class FollowTheQueenApiClientWrapper(IFollowTheQueenApi client) : IGameApiClient
{
	public string GameTypeCode => "FOLLOWTHEQUEEN";

	public async Task<bool> StartGameAsync(Guid gameId)
	{
		var startHandResponse = await client.FollowTheQueenStartHandAsync(gameId);
		if (!startHandResponse.IsSuccessStatusCode) return false;

		var collectAntesResponse = await client.FollowTheQueenCollectAntesAsync(gameId);
		if (!collectAntesResponse.IsSuccessStatusCode) return false;

		var dealHandsResponse = await client.FollowTheQueenDealHandsAsync(gameId);
		return dealHandsResponse.IsSuccessStatusCode;
	}

	public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
	{
		return await client.FollowTheQueenProcessBettingActionAsync(gameId, request);
	}

	public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
	{
		// Follow the Queen is a stud game - no draw phase
		return Task.FromResult(new ProcessDrawResult 
		{ 
			IsSuccess = false, 
			ErrorMessage = "Draw phase not supported for Follow the Queen." 
		});
	}

	public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
	{
		return await client.FollowTheQueenPerformShowdownAsync(gameId);
	}

	public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
	{
		// Follow the Queen does not have Drop or Stay
		return Task.FromResult(false);
	}
}
