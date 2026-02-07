using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services.GameApi;

public interface IGameApiClient
{
    string GameTypeCode { get; }

    Task<bool> StartGameAsync(Guid gameId);
    Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(Guid gameId, ProcessBettingActionRequest request);
    Task<ProcessDrawResult> ProcessDrawAsync(Guid gameId, List<int> discardIndices, Guid playerId);
    Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(Guid gameId);
    
    // For game-specific actions, we can have a flexible method
    Task<bool> DropOrStayAsync(Guid gameId, Guid playerId, string decision);
}

public class ProcessDrawResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public ProcessDrawSuccessful? Content { get; set; }
    public string? NewHandDescription { get; set; }
}
