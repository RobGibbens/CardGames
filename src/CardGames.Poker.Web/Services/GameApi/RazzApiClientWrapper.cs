using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

/// <summary>
/// Razz reuses the Seven Card Stud endpoint family while server-side game code controls rules.
/// </summary>
public sealed class RazzApiClientWrapper(ISevenCardStudApi client) : IGameApiClient
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

    public Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
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
        return client.SevenCardStudPerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        return Task.FromResult(false);
    }
}
