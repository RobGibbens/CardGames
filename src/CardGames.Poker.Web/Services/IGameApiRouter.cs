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
        int playerSeatIndex,
        List<int> discardIndices);

    Task<RouterResponse<Unit>> ProcessBuyCardAsync(string gameCode, Guid gameId, ProcessBuyCardRequest request);

    Task<RouterResponse<Unit>> DropOrStayAsync(string gameCode, Guid gameId, DropOrStayRequest request);

    Task<RouterResponse<Unit>> AcknowledgePotMatchAsync(string gameCode, Guid gameId);

    Task<RouterResponse<Unit>> FoldDuringDrawAsync(Guid gameId, int playerSeatIndex);
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
    private static readonly StringComparer GameCodeComparer = StringComparer.OrdinalIgnoreCase;
    private const string HoldEm = "HOLDEM";
    private const string Omaha = "OMAHA";
    private const string IrishHoldEm = "IRISHHOLDEM";
    private const string PhilsMom = "PHILSMOM";
    private const string TwosJacksManWithTheAxe = "TWOSJACKSMANWITHTHEAXE";
    private const string KingsAndLows = "KINGSANDLOWS";
    private const string SevenCardStud = "SEVENCARDSTUD";
    private const string GoodBadUgly = "GOODBADUGLY";
    private const string Baseball = "BASEBALL";
    private const string HoldTheBaseball = "HOLDTHEBASEBALL";
    private const string FollowTheQueen = "FOLLOWTHEQUEEN";

    private readonly IFiveCardDrawApi _fiveCardDrawApi;
    private readonly ITwosJacksManWithTheAxeApi _twosJacksManWithTheAxeApi;
    private readonly IKingsAndLowsApi _kingsAndLowsApi;
    private readonly ISevenCardStudApi _sevenCardStudApi;
    private readonly IGoodBadUglyApi _goodBadUglyApi;
    private readonly IBaseballApi _baseballApi;
    private readonly IFollowTheQueenApi _followTheQueenApi;
    private readonly IHoldEmApi _holdEmApi;
    private readonly IGamesApi _gamesApi;
    private readonly Dictionary<string, Func<Guid, ProcessBettingActionRequest, Task<RouterResponse<ProcessBettingActionSuccessful>>>> _bettingActionRoutes;
    private readonly Dictionary<string, Func<Guid, Guid, int, List<int>, Task<RouterResponse<ProcessDrawResult>>>> _drawRoutes;
    private readonly Dictionary<string, Func<Guid, DropOrStayRequest, Task<RouterResponse<Unit>>>> _dropOrStayRoutes;
    private readonly Dictionary<string, Func<Guid, ProcessBuyCardRequest, Task<RouterResponse<Unit>>>> _buyCardRoutes;
    private readonly Dictionary<string, Func<Guid, Task<RouterResponse<Unit>>>> _acknowledgePotMatchRoutes;

    public GameApiRouter(
        IFiveCardDrawApi fiveCardDrawApi,
        ITwosJacksManWithTheAxeApi twosJacksManWithTheAxeApi,
        IKingsAndLowsApi kingsAndLowsApi,
        ISevenCardStudApi sevenCardStudApi,
        IGoodBadUglyApi goodBadUglyApi,
        IBaseballApi baseballApi,
        IFollowTheQueenApi followTheQueenApi,
        IHoldEmApi holdEmApi,
        IGamesApi gamesApi)
    {
        _fiveCardDrawApi = fiveCardDrawApi;
        _twosJacksManWithTheAxeApi = twosJacksManWithTheAxeApi;
        _kingsAndLowsApi = kingsAndLowsApi;
        _sevenCardStudApi = sevenCardStudApi;
        _goodBadUglyApi = goodBadUglyApi;
        _baseballApi = baseballApi;
        _followTheQueenApi = followTheQueenApi;
        _holdEmApi = holdEmApi;
        _gamesApi = gamesApi;

        _bettingActionRoutes = new Dictionary<string, Func<Guid, ProcessBettingActionRequest, Task<RouterResponse<ProcessBettingActionSuccessful>>>>(GameCodeComparer)
        {
            [HoldEm] = RouteHoldEmBettingActionAsync,
            [Omaha] = RouteOmahaBettingActionAsync,
            [IrishHoldEm] = RouteIrishHoldEmBettingActionAsync,
            [PhilsMom] = RoutePhilsMomBettingActionAsync,
            [TwosJacksManWithTheAxe] = RouteTwosJacksManWithTheAxeBettingActionAsync,
            [SevenCardStud] = RouteSevenCardStudBettingActionAsync,
            [GoodBadUgly] = RouteGoodBadUglyBettingActionAsync,
            [Baseball] = RouteBaseballBettingActionAsync,
            [HoldTheBaseball] = RouteHoldTheBaseballBettingActionAsync,
            [FollowTheQueen] = RouteFollowTheQueenBettingActionAsync
        };

        _drawRoutes = new Dictionary<string, Func<Guid, Guid, int, List<int>, Task<RouterResponse<ProcessDrawResult>>>>(GameCodeComparer)
        {
            [HoldEm] = RouteHoldEmDrawAsync,
            [Omaha] = RouteOmahaDrawAsync,
            [IrishHoldEm] = RouteIrishHoldEmDrawAsync,
            [PhilsMom] = RoutePhilsMomDrawAsync,
            [TwosJacksManWithTheAxe] = RouteTwosJacksManWithTheAxeDrawAsync,
            [KingsAndLows] = RouteKingsAndLowsDrawAsync
        };

        _dropOrStayRoutes = new Dictionary<string, Func<Guid, DropOrStayRequest, Task<RouterResponse<Unit>>>>(GameCodeComparer)
        {
            [KingsAndLows] = RouteKingsAndLowsDropOrStayAsync
        };

        _buyCardRoutes = new Dictionary<string, Func<Guid, ProcessBuyCardRequest, Task<RouterResponse<Unit>>>>(GameCodeComparer)
        {
            [Baseball] = RouteBaseballBuyCardAsync
        };

        _acknowledgePotMatchRoutes = new Dictionary<string, Func<Guid, Task<RouterResponse<Unit>>>>(GameCodeComparer)
        {
            [KingsAndLows] = RouteKingsAndLowsAcknowledgePotMatchAsync
        };
    }

    public async Task<RouterResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(
        string gameCode,
        Guid gameId,
        ProcessBettingActionRequest request)
    {
        if (_bettingActionRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId, request);
        }

        // Default to Five Card Draw (handles KingsAndLows fallback logic)
        return RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _fiveCardDrawApi.FiveCardDrawProcessBettingActionAsync(gameId, request));
    }

    public async Task<RouterResponse<ProcessDrawResult>> ProcessDrawAsync(
        string gameCode,
        Guid gameId,
        Guid playerId,
        int playerSeatIndex,
        List<int> discardIndices)
    {
        if (_drawRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId, playerId, playerSeatIndex, discardIndices);
        }

        var fcdRequest = new ProcessDrawRequest(discardIndices);
        return RouterResponse<ProcessDrawResult>.FromRefit(
            await _fiveCardDrawApi.FiveCardDrawProcessDrawAsync(gameId, fcdRequest),
            c => new ProcessDrawResult { Original = c });
    }

    public async Task<RouterResponse<Unit>> DropOrStayAsync(string gameCode, Guid gameId, DropOrStayRequest request)
    {
        if (_dropOrStayRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId, request);
        }

        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }

    public async Task<RouterResponse<Unit>> ProcessBuyCardAsync(string gameCode, Guid gameId, ProcessBuyCardRequest request)
    {
        if (_buyCardRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId, request);
        }

        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }

    public async Task<RouterResponse<Unit>> AcknowledgePotMatchAsync(string gameCode, Guid gameId)
    {
        if (_acknowledgePotMatchRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId);
        }

        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteTwosJacksManWithTheAxeBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _twosJacksManWithTheAxeApi.TwosJacksManWithTheAxeProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteSevenCardStudBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _sevenCardStudApi.SevenCardStudProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteGoodBadUglyBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _goodBadUglyApi.GoodBadUglyProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteBaseballBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _baseballApi.BaseballProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteFollowTheQueenBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _followTheQueenApi.FollowTheQueenProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteHoldEmBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _holdEmApi.HoldEmProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteHoldTheBaseballBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _holdEmApi.HoldEmProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteOmahaBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _holdEmApi.HoldEmProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteIrishHoldEmBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _holdEmApi.HoldEmProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RoutePhilsMomBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _holdEmApi.HoldEmProcessBettingActionAsync(gameId, request));

    private Task<RouterResponse<ProcessDrawResult>> RouteHoldEmDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
        => Task.FromResult(
            RouterResponse<ProcessDrawResult>.Failure("Draw phase not supported for Texas Hold'Em.", HttpStatusCode.BadRequest));

    private Task<RouterResponse<ProcessDrawResult>> RouteOmahaDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
        => Task.FromResult(
            RouterResponse<ProcessDrawResult>.Failure("Draw phase not supported for Omaha.", HttpStatusCode.BadRequest));

    private async Task<RouterResponse<ProcessDrawResult>> RouteIrishHoldEmDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
    {
        if (playerSeatIndex < 0)
        {
            return RouterResponse<ProcessDrawResult>.Failure("Player seat is required for Irish Hold 'Em discards.", HttpStatusCode.BadRequest);
        }

        var request = new IrishHoldEmDiscardRequest(discardIndices, playerSeatIndex);
        return RouterResponse<ProcessDrawResult>.FromRefit(
            await _gamesApi.IrishHoldEmDiscardAsync(gameId, request),
            c => new ProcessDrawResult { Original = c });
    }

    private async Task<RouterResponse<ProcessDrawResult>> RoutePhilsMomDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
    {
        if (playerSeatIndex < 0)
        {
            return RouterResponse<ProcessDrawResult>.Failure("Player seat is required for Phil's Mom discards.", HttpStatusCode.BadRequest);
        }

        var request = new IrishHoldEmDiscardRequest(discardIndices, playerSeatIndex);
        return RouterResponse<ProcessDrawResult>.FromRefit(
            await _gamesApi.IrishHoldEmDiscardAsync(gameId, request),
            c => new ProcessDrawResult { Original = c });
    }

    private async Task<RouterResponse<ProcessDrawResult>> RouteTwosJacksManWithTheAxeDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
    {
        var request = new ProcessDrawRequest(discardIndices);
        return RouterResponse<ProcessDrawResult>.FromRefit(
            await _twosJacksManWithTheAxeApi.TwosJacksManWithTheAxeProcessDrawAsync(gameId, request),
            c => new ProcessDrawResult { Original = c });
    }

    private async Task<RouterResponse<ProcessDrawResult>> RouteKingsAndLowsDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
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

    private async Task<RouterResponse<Unit>> RouteKingsAndLowsDropOrStayAsync(Guid gameId, DropOrStayRequest request)
    {
        var response = await _kingsAndLowsApi.KingsAndLowsDropOrStayAsync(gameId, request);
        return RouterResponse<Unit>.FromRefit(response);
    }

    private async Task<RouterResponse<Unit>> RouteBaseballBuyCardAsync(Guid gameId, ProcessBuyCardRequest request)
    {
        var response = await _baseballApi.BaseballProcessBuyCardAsync(gameId, request);
        return RouterResponse<Unit>.FromRefit(response, _ => default(Unit));
    }

    private async Task<RouterResponse<Unit>> RouteKingsAndLowsAcknowledgePotMatchAsync(Guid gameId)
    {
        var response = await _kingsAndLowsApi.KingsAndLowsAcknowledgePotMatchAsync(gameId);
        return RouterResponse<Unit>.FromRefit(response);
    }

    public async Task<RouterResponse<Unit>> FoldDuringDrawAsync(Guid gameId, int playerSeatIndex)
    {
        var request = new IrishHoldEmFoldDuringDrawRequest(playerSeatIndex);
        var response = await _gamesApi.IrishHoldEmFoldDuringDrawAsync(gameId, request);
        return RouterResponse<Unit>.FromRefit(response);
    }
}
