using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class SevenCardStudApiClientWrapper(ISevenCardStudApi client) : IGameApiClient
{
    public string GameTypeCode => "SEVENCARDSTUD";

    public async Task<bool> StartGameAsync(Guid gameId)
    {
        var startHandResponse = await client.SevenCardStudStartHandAsync(gameId);
        if (!startHandResponse.IsSuccessStatusCode) return false;

        var collectAntesResponse = await client.SevenCardStudCollectAntesAsync(gameId);
        if (!collectAntesResponse.IsSuccessStatusCode) return false;

        var dealHandsResponse = await client.SevenCardStudDealHandsAsync(gameId);
        return dealHandsResponse.IsSuccessStatusCode;
    }

    public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
    {
        return await client.SevenCardStudProcessBettingActionAsync(gameId, request);
    }

    public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
    {
        return Task.FromResult(new ProcessDrawResult 
        { 
            IsSuccess = false, 
            ErrorMessage = "Draw phase not supported for Seven Card Stud." 
        });
    }

    public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
    {
        return await client.SevenCardStudPerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        return Task.FromResult(false);
    }
}
