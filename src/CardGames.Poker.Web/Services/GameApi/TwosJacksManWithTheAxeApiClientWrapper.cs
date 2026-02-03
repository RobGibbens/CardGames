using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class TwosJacksManWithTheAxeApiClientWrapper(ITwosJacksManWithTheAxeApi client) : IGameApiClient
{
    public string GameTypeCode => "TWOSJACKSMANWITHTHEAXE";

    public async Task<bool> StartGameAsync(Guid gameId)
    {
        var startHandResponse = await client.TwosJacksManWithTheAxeStartHandAsync(gameId);
        if (!startHandResponse.IsSuccessStatusCode) return false;

        var collectAntesResponse = await client.TwosJacksManWithTheAxeCollectAntesAsync(gameId);
        if (!collectAntesResponse.IsSuccessStatusCode) return false;

        var dealHandsResponse = await client.TwosJacksManWithTheAxeDealHandsAsync(gameId);
        return dealHandsResponse.IsSuccessStatusCode;
    }

    public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
    {
        return await client.TwosJacksManWithTheAxeProcessBettingActionAsync(gameId, request);
    }

    public async Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
    {
        var request = new ProcessDrawRequest(discardIndices);
        var response = await client.TwosJacksManWithTheAxeProcessDrawAsync(gameId, request);
        
        return new ProcessDrawResult
        {
            IsSuccess = response.IsSuccessStatusCode,
            ErrorMessage = response.Error?.Content,
            Content = response.Content
        };
    }

    public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
    {
        return await client.TwosJacksManWithTheAxePerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        return Task.FromResult(false);
    }
}
