using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;
using System.Net;

namespace CardGames.Poker.Web.Services;

public interface IGameApiRouter
{
    Task<RouterResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(
        string gameCode,
        Guid gameId,
        ProcessBettingActionRequest request);

    Task<RouterResponse<ProcessDrawResult>> ProcessDrawAsync(
        string gameCode,
        Guid gameId,
        Guid playerId,
        List<int> discardIndices);

    Task<RouterResponse<Unit>> ProcessBuyCardAsync(string gameCode, Guid gameId, ProcessBuyCardRequest request);

    Task<RouterResponse<Unit>> DropOrStayAsync(string gameCode, Guid gameId, DropOrStayRequest request);

    Task<RouterResponse<Unit>> AcknowledgePotMatchAsync(string gameCode, Guid gameId);
}

public class ProcessDrawResult
{
    public ProcessDrawSuccessful Original { get; set; } = default!;
    public string? NewHandDescription { get; set; }
}

public class RouterResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Content { get; set; }
    public string? Error { get; set; }
    public HttpStatusCode StatusCode { get; set; }

    public static RouterResponse<T> Success(T content, HttpStatusCode statusCode = HttpStatusCode.OK) => new()
    {
        IsSuccess = true,
        Content = content,
        StatusCode = statusCode
    };

    public static RouterResponse<T> Failure(string? error, HttpStatusCode statusCode) => new()
    {
        IsSuccess = false,
        Error = error,
        StatusCode = statusCode
    };

    public static RouterResponse<T> FromRefit<TRefit>(IApiResponse<TRefit> response, Func<TRefit, T>? mapper = null)
    {
        if (!response.IsSuccessStatusCode)
        {
            return Failure(response.Error?.Content ?? response.Error?.Message, response.StatusCode);
        }

        T content;
        if (mapper != null)
        {
            content = mapper(response.Content!);
        }
        else if (response.Content is T t)
        {
            content = t;
        }
        else
        {
            throw new InvalidOperationException($"Cannot map {typeof(TRefit).Name} to {typeof(T).Name}");
        }

        return Success(content, response.StatusCode);
    }
    
    public static RouterResponse<T> FromRefit(IApiResponse response) 
    {
         if (!response.IsSuccessStatusCode)
        {
            return Failure(response.Error?.Content ?? response.Error?.Message, response.StatusCode);
        }
        return Success(default!, response.StatusCode);
    }
}

public struct Unit { }

public class GameApiRouter : IGameApiRouter
{
    private readonly IFiveCardDrawApi _fiveCardDrawApi;
    private readonly ITwosJacksManWithTheAxeApi _twosJacksManWithTheAxeApi;
    private readonly IKingsAndLowsApi _kingsAndLowsApi;
    private readonly ISevenCardStudApi _sevenCardStudApi;
    private readonly IBaseballApi _baseballApi;

    public GameApiRouter(
        IFiveCardDrawApi fiveCardDrawApi,
        ITwosJacksManWithTheAxeApi twosJacksManWithTheAxeApi,
        IKingsAndLowsApi kingsAndLowsApi,
        ISevenCardStudApi sevenCardStudApi,
        IBaseballApi baseballApi)
    {
        _fiveCardDrawApi = fiveCardDrawApi;
        _twosJacksManWithTheAxeApi = twosJacksManWithTheAxeApi;
        _kingsAndLowsApi = kingsAndLowsApi;
        _sevenCardStudApi = sevenCardStudApi;
        _baseballApi = baseballApi;
    }

    public async Task<RouterResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(
        string gameCode,
        Guid gameId,
        ProcessBettingActionRequest request)
    {
        if (string.Equals(gameCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
        {
            return RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
                await _twosJacksManWithTheAxeApi.TwosJacksManWithTheAxeProcessBettingActionAsync(gameId, request));
        }
        else if (string.Equals(gameCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase))
        {
            return RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
                await _sevenCardStudApi.SevenCardStudProcessBettingActionAsync(gameId, request));
        }
        else if (string.Equals(gameCode, "BASEBALL", StringComparison.OrdinalIgnoreCase))
        {
            return RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
                await _baseballApi.BaseballProcessBettingActionAsync(gameId, request));
        }
        
        // Default to Five Card Draw (handles KingsAndLows fallback logic)
        return RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _fiveCardDrawApi.FiveCardDrawProcessBettingActionAsync(gameId, request));
    }

    public async Task<RouterResponse<ProcessDrawResult>> ProcessDrawAsync(
        string gameCode,
        Guid gameId,
        Guid playerId,
        List<int> discardIndices)
    {
        if (string.Equals(gameCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
        {
            var request = new ProcessDrawRequest(discardIndices);
            return RouterResponse<ProcessDrawResult>.FromRefit(
                await _twosJacksManWithTheAxeApi.TwosJacksManWithTheAxeProcessDrawAsync(gameId, request),
                c => new ProcessDrawResult { Original = c });
        }
        else if (string.Equals(gameCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
        {
            var request = new DrawCardsRequest(discardIndices, playerId);
            var response = await _kingsAndLowsApi.KingsAndLowsDrawCardsAsync(gameId, request);
            
            return RouterResponse<ProcessDrawResult>.FromRefit(response, content => new ProcessDrawResult
            {
                Original = new ProcessDrawSuccessful(
                    currentPhase: content.NextPhase ?? "DrawPhase",
                    discardedCards: content.DiscardedCards,
                    drawComplete: content.DrawPhaseComplete,
                    gameId: content.GameId,
                    newCards: content.NewCards ?? [],
                    nextDrawPlayerIndex: -1,
                    nextDrawPlayerName: content.NextPlayerName,
                    playerName: content.PlayerName ?? "",
                    playerSeatIndex: content.PlayerSeatIndex
                ),
                NewHandDescription = content.NewHandDescription
            });
        }
        
        var fcdRequest = new ProcessDrawRequest(discardIndices);
        return RouterResponse<ProcessDrawResult>.FromRefit(
            await _fiveCardDrawApi.FiveCardDrawProcessDrawAsync(gameId, fcdRequest),
            c => new ProcessDrawResult { Original = c });
    }

    public async Task<RouterResponse<Unit>> DropOrStayAsync(string gameCode, Guid gameId, DropOrStayRequest request)
    {
        if (string.Equals(gameCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
        {
            var response = await _kingsAndLowsApi.KingsAndLowsDropOrStayAsync(gameId, request);
            return RouterResponse<Unit>.FromRefit(response);
        }
        
        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }

    public async Task<RouterResponse<Unit>> ProcessBuyCardAsync(string gameCode, Guid gameId, ProcessBuyCardRequest request)
    {
        if (string.Equals(gameCode, "BASEBALL", StringComparison.OrdinalIgnoreCase))
        {
            var response = await _baseballApi.BaseballProcessBuyCardAsync(gameId, request);
            return RouterResponse<Unit>.FromRefit(response, _ => default(Unit));
        }

        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }

    public async Task<RouterResponse<Unit>> AcknowledgePotMatchAsync(string gameCode, Guid gameId)
    {
        if (string.Equals(gameCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
        {
            var response = await _kingsAndLowsApi.KingsAndLowsAcknowledgePotMatchAsync(gameId);
            return RouterResponse<Unit>.FromRefit(response);
        }
        
        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }
}
