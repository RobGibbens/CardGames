using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardGames.Poker.Api.Contracts;
using Refit;
using System.Linq;

namespace CardGames.Poker.Web.Services.GameApi;

public interface IGameApiRouter
{
    Task<bool> StartGameAsync(string gameTypeCode, Guid gameId);
    Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(string gameTypeCode, Guid gameId, ProcessBettingActionRequest request);
    Task<ProcessDrawResult> ProcessDrawAsync(string gameTypeCode, Guid gameId, List<int> discardIndices, Guid playerId);
    Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(string gameTypeCode, Guid gameId);
    Task<bool> DropOrStayAsync(string gameTypeCode, Guid gameId, Guid playerId, string decision);
}

public class GameApiRouter : IGameApiRouter
{
    private readonly IEnumerable<IGameApiClient> _clients;

    public GameApiRouter(IEnumerable<IGameApiClient> clients)
    {
        _clients = clients;
    }

    private IGameApiClient GetClient(string gameTypeCode)
    {
        return _clients.FirstOrDefault(c => c.GameTypeCode.Equals(gameTypeCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotSupportedException(
                $"No IGameApiClient registered for game type '{gameTypeCode}'. " +
                "Register an IGameApiClient implementation for this game type.");
    }

    public Task<bool> StartGameAsync(string gameTypeCode, Guid gameId)
    {
        return GetClient(gameTypeCode).StartGameAsync(gameId);
    }

    public Task<IApiResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(string gameTypeCode, Guid gameId, ProcessBettingActionRequest request)
    {
        return GetClient(gameTypeCode).ProcessBettingActionAsync(gameId, request);
    }

    public Task<ProcessDrawResult> ProcessDrawAsync(string gameTypeCode, Guid gameId, List<int> discardIndices, Guid playerId)
    {
        return GetClient(gameTypeCode).ProcessDrawAsync(gameId, discardIndices, playerId);
    }

    public Task<IApiResponse<PerformShowdownSuccessful>> PerformShowdownAsync(string gameTypeCode, Guid gameId)
    {
        return GetClient(gameTypeCode).PerformShowdownAsync(gameId);
    }

    public Task<bool> DropOrStayAsync(string gameTypeCode, Guid gameId, Guid playerId, string decision)
    {
        return GetClient(gameTypeCode).DropOrStayAsync(gameId, playerId, decision);
    }
}
