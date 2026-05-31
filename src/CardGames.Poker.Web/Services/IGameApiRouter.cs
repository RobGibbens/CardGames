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

    Task<RouterResponse<Unit>> KeepOrTradeAsync(string gameCode, Guid gameId, KeepOrTradeRequest request);

    Task<RouterResponse<Unit>> AcknowledgePotMatchAsync(string gameCode, Guid gameId);

    Task<RouterResponse<TollboothChooseCardSuccessful>> TollboothChooseCardAsync(Guid gameId, TollboothChooseCardRequest request);

    Task<RouterResponse<Unit>> FoldDuringDrawAsync(Guid gameId, int playerSeatIndex);

    Task<RouterResponse<InBetweenAceChoiceSuccessful>> InBetweenAceChoiceAsync(Guid gameId, InBetweenAceChoiceRequest request);

    Task<RouterResponse<InBetweenPlaceBetSuccessful>> InBetweenPlaceBetAsync(Guid gameId, InBetweenPlaceBetRequest request);
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
        else if (typeof(T) == typeof(Unit))
        {
            content = (T)(object)default(Unit);
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

/// <summary>
/// Active web-side game router. Translates a game's stable game code (for example
/// <c>"HOLDEM"</c>) into the correct backend API call for each player action
/// (betting, draw, drop-or-stay, keep-or-trade, buy-card, acknowledge-pot-match).
/// </summary>
/// <remarks>
/// <para><b>Design.</b> Routing uses one case-insensitive dispatch dictionary per action
/// kind (for example <see cref="_bettingActionRoutes"/> and <see cref="_drawRoutes"/>).
/// Each dictionary maps a game code constant to a small <c>Route…Async</c> method that
/// calls the relevant Refit API and normalizes the result through
/// <see cref="RouterResponse{T}.FromRefit{TRefit}(Refit.IApiResponse{TRefit}, System.Func{TRefit, T})"/>.
/// The dictionaries are the single, auditable source of truth for which game supports
/// which action: to see everything a game supports, search the maps for its constant.</para>
///
/// <para><b>Behavior when a game code is missing.</b> Betting and draw are required for
/// every active game, so a missing entry throws <see cref="NotSupportedException"/> to
/// surface the gap loudly. The optional actions (drop-or-stay, keep-or-trade, buy-card,
/// acknowledge-pot-match) return a <see cref="RouterResponse{T}.Failure"/> because most
/// games legitimately do not support them.</para>
///
/// <para><b>Adding a new game variant.</b> Follow this checklist so the variant is wired
/// up consistently in every place it is needed:</para>
/// <list type="number">
///   <item>Add a <c>private const string</c> game code constant in the region below.</item>
///   <item>Add a <c>Route…Async</c> method (or reuse an existing one — Hold 'Em-family
///   variants reuse <see cref="RouteHoldEmBettingActionAsync"/>).</item>
///   <item>Register the variant in every action dictionary it participates in. Omitting an
///   entry is the most common source of "works for some actions but not others" bugs.</item>
///   <item>Add or update a test in <c>GameApiRouterTests</c> asserting the new mapping.</item>
/// </list>
/// <para>See <c>docs/WebRouterDesign.md</c> for the repo-facing description of this design,
/// and <c>docs/GameVariantBoundary.md</c> for the full cross-layer variant onboarding checklist.</para>
/// </remarks>
public class GameApiRouter : IGameApiRouter
{
    private static readonly StringComparer GameCodeComparer = StringComparer.OrdinalIgnoreCase;
    private const string HoldEm = "HOLDEM";
    private const string RedRiver = "REDRIVER";
    private const string Omaha = "OMAHA";
    private const string Nebraska = "NEBRASKA";
    private const string SouthDakota = "SOUTHDAKOTA";
    private const string IrishHoldEm = "IRISHHOLDEM";
    private const string PhilsMom = "PHILSMOM";
    private const string CrazyPineapple = "CRAZYPINEAPPLE";
    private const string BobBarker = "BOBBARKER";
    private const string TwosJacksManWithTheAxe = "TWOSJACKSMANWITHTHEAXE";
    private const string KingsAndLows = "KINGSANDLOWS";
    private const string SevenCardStud = "SEVENCARDSTUD";
    private const string PairPressure = "PAIRPRESSURE";
    private const string Razz = "RAZZ";
    private const string GoodBadUgly = "GOODBADUGLY";
    private const string Baseball = "BASEBALL";
    private const string HoldTheBaseball = "HOLDTHEBASEBALL";
    private const string FollowTheQueen = "FOLLOWTHEQUEEN";
    private const string ScrewYourNeighbor = "SCREWYOURNEIGHBOR";
    private const string Tollbooth = "TOLLBOOTH";
    private const string FiveCardDraw = "FIVECARDDRAW";
    private const string Klondike = "KLONDIKE";
    private const string InBetween = "INBETWEEN";

    private readonly IFiveCardDrawApi _fiveCardDrawApi;
    private readonly ITwosJacksManWithTheAxeApi _twosJacksManWithTheAxeApi;
    private readonly IKingsAndLowsApi _kingsAndLowsApi;
    private readonly ISevenCardStudApi _sevenCardStudApi;
    private readonly IPairPressureApi _pairPressureApi;
    private readonly IGoodBadUglyApi _goodBadUglyApi;
    private readonly IBaseballApi _baseballApi;
    private readonly IFollowTheQueenApi _followTheQueenApi;
    private readonly IHoldEmApi _holdEmApi;
    private readonly IGamesApi _gamesApi;
    private readonly IScrewYourNeighborApi _screwYourNeighborApi;
    private readonly ITollboothApi _tollboothApi;
    private readonly IInBetweenApi _inBetweenApi;
    private readonly Dictionary<string, Func<Guid, ProcessBettingActionRequest, Task<RouterResponse<ProcessBettingActionSuccessful>>>> _bettingActionRoutes;
    private readonly Dictionary<string, Func<Guid, Guid, int, List<int>, Task<RouterResponse<ProcessDrawResult>>>> _drawRoutes;
    private readonly Dictionary<string, Func<Guid, DropOrStayRequest, Task<RouterResponse<Unit>>>> _dropOrStayRoutes;
    private readonly Dictionary<string, Func<Guid, KeepOrTradeRequest, Task<RouterResponse<Unit>>>> _keepOrTradeRoutes;
    private readonly Dictionary<string, Func<Guid, ProcessBuyCardRequest, Task<RouterResponse<Unit>>>> _buyCardRoutes;
    private readonly Dictionary<string, Func<Guid, Task<RouterResponse<Unit>>>> _acknowledgePotMatchRoutes;

    public GameApiRouter(
        IFiveCardDrawApi fiveCardDrawApi,
        ITwosJacksManWithTheAxeApi twosJacksManWithTheAxeApi,
        IKingsAndLowsApi kingsAndLowsApi,
        ISevenCardStudApi sevenCardStudApi,
        IPairPressureApi pairPressureApi,
        IGoodBadUglyApi goodBadUglyApi,
        IBaseballApi baseballApi,
        IFollowTheQueenApi followTheQueenApi,
        IHoldEmApi holdEmApi,
        IGamesApi gamesApi,
        IScrewYourNeighborApi screwYourNeighborApi,
        ITollboothApi tollboothApi,
        IInBetweenApi inBetweenApi)
    {
        _fiveCardDrawApi = fiveCardDrawApi;
        _twosJacksManWithTheAxeApi = twosJacksManWithTheAxeApi;
        _kingsAndLowsApi = kingsAndLowsApi;
        _sevenCardStudApi = sevenCardStudApi;
        _pairPressureApi = pairPressureApi;
        _goodBadUglyApi = goodBadUglyApi;
        _baseballApi = baseballApi;
        _followTheQueenApi = followTheQueenApi;
        _holdEmApi = holdEmApi;
        _gamesApi = gamesApi;
        _screwYourNeighborApi = screwYourNeighborApi;
        _tollboothApi = tollboothApi;
        _inBetweenApi = inBetweenApi;

        // Betting-action dispatch table. Each game code maps to the route that forwards
        // the betting action to the correct API. Hold 'Em-family variants share the Hold 'Em
        // betting endpoint, so they all point at the single RouteHoldEmBettingActionAsync method.
        _bettingActionRoutes = new Dictionary<string, Func<Guid, ProcessBettingActionRequest, Task<RouterResponse<ProcessBettingActionSuccessful>>>>(GameCodeComparer)
        {
            // Hold 'Em-family variants (all forwarded to the Hold 'Em betting endpoint).
            [HoldEm] = RouteHoldEmBettingActionAsync,
            [RedRiver] = RouteHoldEmBettingActionAsync,
            [Omaha] = RouteHoldEmBettingActionAsync,
            [Nebraska] = RouteHoldEmBettingActionAsync,
            [SouthDakota] = RouteHoldEmBettingActionAsync,
            [BobBarker] = RouteHoldEmBettingActionAsync,
            [IrishHoldEm] = RouteHoldEmBettingActionAsync,
            [PhilsMom] = RouteHoldEmBettingActionAsync,
            [CrazyPineapple] = RouteHoldEmBettingActionAsync,
            [HoldTheBaseball] = RouteHoldEmBettingActionAsync,
            [Klondike] = RouteHoldEmBettingActionAsync,
            // Variants with their own betting endpoints.
            [TwosJacksManWithTheAxe] = RouteTwosJacksManWithTheAxeBettingActionAsync,
            [SevenCardStud] = RouteSevenCardStudBettingActionAsync,
            [PairPressure] = RoutePairPressureBettingActionAsync,
            [Razz] = RouteSevenCardStudBettingActionAsync,
            [GoodBadUgly] = RouteGoodBadUglyBettingActionAsync,
            [Baseball] = RouteBaseballBettingActionAsync,
            [FollowTheQueen] = RouteFollowTheQueenBettingActionAsync,
            [Tollbooth] = RouteTollboothBettingActionAsync,
            [FiveCardDraw] = RouteFiveCardDrawBettingActionAsync
        };

        _drawRoutes = new Dictionary<string, Func<Guid, Guid, int, List<int>, Task<RouterResponse<ProcessDrawResult>>>>(GameCodeComparer)
        {
            [HoldEm] = RouteHoldEmDrawAsync,
            [Omaha] = RouteOmahaDrawAsync,
            [Nebraska] = RouteNebraskaDrawAsync,
            [SouthDakota] = RouteSouthDakotaDrawAsync,
            [BobBarker] = RouteBobBarkerDrawAsync,
            [IrishHoldEm] = RouteIrishHoldEmDrawAsync,
            [PhilsMom] = RoutePhilsMomDrawAsync,
            [CrazyPineapple] = RouteCrazyPineappleDrawAsync,
            [TwosJacksManWithTheAxe] = RouteTwosJacksManWithTheAxeDrawAsync,
            [KingsAndLows] = RouteKingsAndLowsDrawAsync,
            [FiveCardDraw] = RouteFiveCardDrawDrawAsync
        };

        _dropOrStayRoutes = new Dictionary<string, Func<Guid, DropOrStayRequest, Task<RouterResponse<Unit>>>>(GameCodeComparer)
        {
            [KingsAndLows] = RouteKingsAndLowsDropOrStayAsync
        };

        _keepOrTradeRoutes = new Dictionary<string, Func<Guid, KeepOrTradeRequest, Task<RouterResponse<Unit>>>>(GameCodeComparer)
        {
            [ScrewYourNeighbor] = RouteScrewYourNeighborKeepOrTradeAsync
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

    /// <summary>
    /// Game codes wired into the <b>betting</b> action family. Betting is mandatory for every
    /// standard variant, so this is the canonical "is this variant wired into the active web
    /// router?" check. The boundary test in <c>GameVariantBoundaryTests</c> asserts every domain
    /// variant either appears here or is an explicitly documented special-action variant.
    /// </summary>
    public IReadOnlyCollection<string> SupportedBettingGameCodes => _bettingActionRoutes.Keys;

    /// <summary>Game codes wired into the <b>draw / discard</b> action family.</summary>
    public IReadOnlyCollection<string> SupportedDrawGameCodes => _drawRoutes.Keys;

    /// <summary>Game codes wired into the optional <b>drop-or-stay</b> action family.</summary>
    public IReadOnlyCollection<string> SupportedDropOrStayGameCodes => _dropOrStayRoutes.Keys;

    /// <summary>Game codes wired into the optional <b>keep-or-trade</b> action family.</summary>
    public IReadOnlyCollection<string> SupportedKeepOrTradeGameCodes => _keepOrTradeRoutes.Keys;

    /// <summary>Game codes wired into the optional <b>buy-card</b> action family.</summary>
    public IReadOnlyCollection<string> SupportedBuyCardGameCodes => _buyCardRoutes.Keys;

    /// <summary>Game codes wired into the optional <b>acknowledge-pot-match</b> action family.</summary>
    public IReadOnlyCollection<string> SupportedAcknowledgePotMatchGameCodes => _acknowledgePotMatchRoutes.Keys;

    public async Task<RouterResponse<ProcessBettingActionSuccessful>> ProcessBettingActionAsync(
        string gameCode,
        Guid gameId,
        ProcessBettingActionRequest request)
    {
        if (_bettingActionRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId, request);
        }

        throw new NotSupportedException(
            $"No betting action route configured for game type '{gameCode}'. " +
            "Add an explicit entry in _bettingActionRoutes for this game type.");
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

        throw new NotSupportedException(
            $"No draw route configured for game type '{gameCode}'. " +
            "Add an explicit entry in _drawRoutes for this game type.");
    }

    public async Task<RouterResponse<Unit>> DropOrStayAsync(string gameCode, Guid gameId, DropOrStayRequest request)
    {
        if (_dropOrStayRoutes.TryGetValue(gameCode, out var route))
        {
            return await route(gameId, request);
        }

        return RouterResponse<Unit>.Failure("Not supported for this game type", HttpStatusCode.BadRequest);
    }

    public async Task<RouterResponse<Unit>> KeepOrTradeAsync(string gameCode, Guid gameId, KeepOrTradeRequest request)
    {
        if (_keepOrTradeRoutes.TryGetValue(gameCode, out var route))
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

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RoutePairPressureBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _pairPressureApi.PairPressureProcessBettingActionAsync(gameId, request));

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

    private Task<RouterResponse<ProcessDrawResult>> RouteHoldEmDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
        => Task.FromResult(
            RouterResponse<ProcessDrawResult>.Failure("Draw phase not supported for Texas Hold'Em.", HttpStatusCode.BadRequest));

    private Task<RouterResponse<ProcessDrawResult>> RouteOmahaDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
        => Task.FromResult(
            RouterResponse<ProcessDrawResult>.Failure("Draw phase not supported for Omaha.", HttpStatusCode.BadRequest));

    private Task<RouterResponse<ProcessDrawResult>> RouteNebraskaDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
        => Task.FromResult(
            RouterResponse<ProcessDrawResult>.Failure("Draw phase not supported for Nebraska.", HttpStatusCode.BadRequest));

    private Task<RouterResponse<ProcessDrawResult>> RouteSouthDakotaDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
        => Task.FromResult(
            RouterResponse<ProcessDrawResult>.Failure("Draw phase not supported for South Dakota.", HttpStatusCode.BadRequest));

    private async Task<RouterResponse<ProcessDrawResult>> RouteBobBarkerDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
    {
        if (playerSeatIndex < 0)
        {
            return RouterResponse<ProcessDrawResult>.Failure("Player seat is required for Bob Barker showcase selection.", HttpStatusCode.BadRequest);
        }

        if (discardIndices.Count != 1)
        {
            return RouterResponse<ProcessDrawResult>.Failure("Select exactly one showcase card.", HttpStatusCode.BadRequest);
        }

        var request = new BobBarkerSelectShowcaseRequest(discardIndices[0], playerSeatIndex);
        var response = await _gamesApi.BobBarkerSelectShowcaseAsync(gameId, request);
        if (!response.IsSuccessStatusCode)
        {
            return RouterResponse<ProcessDrawResult>.Failure(response.Error?.Content ?? response.Error?.Message, response.StatusCode);
        }

        return RouterResponse<ProcessDrawResult>.Success(new ProcessDrawResult
        {
            Original = new ProcessDrawSuccessful(
                currentPhase: "DrawPhase",
                discardedCards: [],
                drawComplete: false,
                gameId: gameId,
                newCards: [],
                nextDrawPlayerIndex: null,
                nextDrawPlayerName: string.Empty,
                playerName: string.Empty,
                playerSeatIndex: playerSeatIndex)
        }, response.StatusCode);
    }

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

    private async Task<RouterResponse<ProcessDrawResult>> RouteCrazyPineappleDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
    {
        if (playerSeatIndex < 0)
        {
            return RouterResponse<ProcessDrawResult>.Failure("Player seat is required for Crazy Pineapple discards.", HttpStatusCode.BadRequest);
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

    private async Task<RouterResponse<Unit>> RouteScrewYourNeighborKeepOrTradeAsync(Guid gameId, KeepOrTradeRequest request)
    {
        var response = await _screwYourNeighborApi.ScrewYourNeighborKeepOrTradeAsync(gameId, request);
        return RouterResponse<Unit>.FromRefit(response);
    }

    public async Task<RouterResponse<Unit>> FoldDuringDrawAsync(Guid gameId, int playerSeatIndex)
    {
        var request = new IrishHoldEmFoldDuringDrawRequest(playerSeatIndex);
        var response = await _gamesApi.IrishHoldEmFoldDuringDrawAsync(gameId, request);
        return RouterResponse<Unit>.FromRefit(response);
    }

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteTollboothBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _tollboothApi.TollboothProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessBettingActionSuccessful>> RouteFiveCardDrawBettingActionAsync(Guid gameId, ProcessBettingActionRequest request)
        => RouterResponse<ProcessBettingActionSuccessful>.FromRefit(
            await _fiveCardDrawApi.FiveCardDrawProcessBettingActionAsync(gameId, request));

    private async Task<RouterResponse<ProcessDrawResult>> RouteFiveCardDrawDrawAsync(Guid gameId, Guid playerId, int playerSeatIndex, List<int> discardIndices)
    {
        var fcdRequest = new ProcessDrawRequest(discardIndices);
        return RouterResponse<ProcessDrawResult>.FromRefit(
            await _fiveCardDrawApi.FiveCardDrawProcessDrawAsync(gameId, fcdRequest),
            c => new ProcessDrawResult { Original = c });
    }

    public async Task<RouterResponse<TollboothChooseCardSuccessful>> TollboothChooseCardAsync(Guid gameId, TollboothChooseCardRequest request)
        => RouterResponse<TollboothChooseCardSuccessful>.FromRefit(
            await _tollboothApi.TollboothChooseCardAsync(gameId, request));

    public async Task<RouterResponse<InBetweenAceChoiceSuccessful>> InBetweenAceChoiceAsync(Guid gameId, InBetweenAceChoiceRequest request)
        => RouterResponse<InBetweenAceChoiceSuccessful>.FromRefit(
            await _inBetweenApi.InBetweenAceChoiceAsync(gameId, request));

    public async Task<RouterResponse<InBetweenPlaceBetSuccessful>> InBetweenPlaceBetAsync(Guid gameId, InBetweenPlaceBetRequest request)
        => RouterResponse<InBetweenPlaceBetSuccessful>.FromRefit(
            await _inBetweenApi.InBetweenPlaceBetAsync(gameId, request));
}
