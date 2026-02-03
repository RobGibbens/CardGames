using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public class KingsAndLowsApiClientWrapper(IKingsAndLowsApi client, IFiveCardDrawApi fiveCardDrawClient) : IGameApiClient
{
    public string GameTypeCode => "KINGSANDLOWS";

    public async Task<bool> StartGameAsync(Guid gameId)
    {
        var response = await client.KingsAndLowsStartHandAsync(gameId);
        return response.IsSuccessStatusCode;
    }

    public async Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
    {
        // Fallback to FiveCardDraw betting action as per legacy implementation
        return await fiveCardDrawClient.FiveCardDrawProcessBettingActionAsync(gameId, request);
    }

    public async Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId)
    {
        var request = new DrawCardsRequest(discardIndices, playerId);
        var response = await client.KingsAndLowsDrawCardsAsync(gameId, request);
        
        var result = new ProcessDrawResult
        {
            IsSuccess = response.IsSuccessStatusCode,
            ErrorMessage = response.Error?.Content,
            NewHandDescription = response.Content?.NewHandDescription
        };

        if (response.IsSuccessStatusCode && response.Content != null)
        {
            var content = response.Content;
            result.Content = new ProcessDrawSuccessful(
                currentPhase: content.NextPhase ?? "DrawPhase",
                discardedCards: content.DiscardedCards,
                drawComplete: content.DrawPhaseComplete,
                gameId: content.GameId,
                newCards: content.NewCards ?? [],
                nextDrawPlayerIndex: -1,
                nextDrawPlayerName: content.NextPlayerName,
                playerName: content.PlayerName ?? "",
                playerSeatIndex: content.PlayerSeatIndex
            );
        }

        return result;
    }

    public async Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId)
    {
        return await client.KingsAndLowsPerformShowdownAsync(gameId);
    }

    public async Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision)
    {
        var request = new DropOrStayRequest(decision, playerId);
        var response = await client.KingsAndLowsDropOrStayAsync(gameId, request);
        return response.IsSuccessStatusCode;
    }
}
