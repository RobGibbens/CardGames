using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class HoldEmApiClientWrapper(IHoldEmApi client) : IGameApiClient
{
    public string GameTypeCode => "HOLDEM";

    public async Task<bool> StartGameAsync(Guid gameId)
    {
        // Hold'Em start endpoint handles the full sequence:
        // shuffle, post blinds, deal hole cards, begin pre-flop betting.
        var startResponse = await client.HoldEmStartHandAsync(gameId);
        return startResponse.IsSuccessStatusCode;
    }

    public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
    {
        return await client.HoldEmProcessBettingActionAsync(gameId, request);
    }

    public Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
    {
        return Task.FromResult(new ProcessDrawResult
        {
            IsSuccess = false,
            ErrorMessage = "Draw phase not supported for Texas Hold'Em."
        });
    }

    public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
    {
        return await client.HoldEmPerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        return Task.FromResult(false);
    }
}
