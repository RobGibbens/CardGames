using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.Cache;
using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// SignalR hub for real-time poker game state updates.
/// Clients join per-game groups to receive table state broadcasts.
/// </summary>
[Authorize(AuthenticationSchemes = HeaderAuthenticationHandler.SchemeName)]
public sealed class GameHub : Hub
{
    private readonly ITableStateBuilder _tableStateBuilder;
    private readonly IActiveGameCache _activeGameCache;
    private readonly IGameStateManager _gameStateManager;
    private readonly IOptions<InMemoryEngineOptions> _engineOptions;
    private readonly IOptions<ActiveGameCacheOptions> _cacheOptions;
    private readonly ILogger<GameHub> _logger;

    /// <summary>
    /// Group name prefix for game groups.
    /// </summary>
    private const string GameGroupPrefix = "game:";

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHub"/> class.
    /// </summary>
    public GameHub(
        ITableStateBuilder tableStateBuilder,
        IActiveGameCache activeGameCache,
        IGameStateManager gameStateManager,
        IOptions<InMemoryEngineOptions> engineOptions,
        IOptions<ActiveGameCacheOptions> cacheOptions,
        ILogger<GameHub> logger)
    {
        _tableStateBuilder = tableStateBuilder;
        _activeGameCache = activeGameCache;
        _gameStateManager = gameStateManager;
        _engineOptions = engineOptions;
        _cacheOptions = cacheOptions;
        _logger = logger;
    }

    /// <summary>
    /// Joins the caller to the specified game group and sends initial state snapshot.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game to join.</param>
    public async Task JoinGame(Guid gameId)
    {
        var userId = Context.UserIdentifier ?? GetUserIdentifier();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("User attempted to join game {GameId} without valid identifier", gameId);
            throw new HubException("User identifier not found");
        }

        var groupName = GetGroupName(gameId);

        // Add the caller to the game group
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "User {UserId} (connection {ConnectionId}) joined game group {GroupName}",
            userId, Context.ConnectionId, groupName);

        // Send initial state snapshot to the caller
        await SendStateSnapshotToCallerAsync(gameId, userId);
    }

    /// <summary>
    /// Removes the caller from the specified game group.
    /// </summary>
    /// <param name="gameId">The unique identifier of the game to leave.</param>
    public async Task LeaveGame(Guid gameId)
    {
        var userId = Context.UserIdentifier ?? GetUserIdentifier();
        var groupName = GetGroupName(gameId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "User {UserId} (connection {ConnectionId}) left game group {GroupName}",
            userId ?? "unknown", Context.ConnectionId, groupName);
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? GetUserIdentifier();
        _logger.LogInformation("User {UserId} connected with connection {ConnectionId}",
            userId ?? "unknown", Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdentifier();

        if (exception is not null)
        {
            _logger.LogWarning(exception,
                "User {UserId} disconnected with error (connection {ConnectionId})",
                userId ?? "unknown", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("User {UserId} disconnected (connection {ConnectionId})",
                userId ?? "unknown", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends the current state snapshot to the calling client.
    /// Serves from the active game cache when available to avoid DB round-trips on reconnect.
    /// </summary>
    private async Task SendStateSnapshotToCallerAsync(Guid gameId, string userId)
    {
        try
        {
            _logger.LogDebug(
                "Sending state snapshot for game {GameId} to caller user {UserId} (connection {ConnectionId})",
                gameId, userId, Context.ConnectionId);

            var privateStateUserId = Context.UserIdentifier ?? userId;

            // Try serving from cache first
            if (_cacheOptions.Value.ServeReconnectsFromCache
                && _activeGameCache.TryGet(gameId, out var snapshot))
            {
                _logger.LogDebug("Serving cached snapshot for game {GameId} to user {UserId}", gameId, privateStateUserId);

                await Clients.Caller.SendAsync("TableStateUpdated", snapshot.PublicState);

                if (snapshot.PrivateStatesByUserId.TryGetValue(privateStateUserId, out var cachedPrivate))
                {
                    await Clients.Caller.SendAsync("PrivateStateUpdated", cachedPrivate);
                }
                else
                {
                    // Partial hit: public cached, private missing — fall back to DB for this user only
                    _logger.LogDebug(
                        "Private state not cached for user {UserId} in game {GameId}; rebuilding from DB",
                        privateStateUserId, gameId);
                    var privateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, privateStateUserId);
                    if (privateState is not null)
                    {
                        await Clients.Caller.SendAsync("PrivateStateUpdated", privateState);
                        _activeGameCache.UpsertPrivateState(gameId, privateStateUserId, privateState, snapshot.VersionNumber);
                    }
                }

                return;
            }

            // Cache miss — fall back to full DB build
            // When in-memory engine is active, ensure the game is loaded into GameStateManager
            // so subsequent operations don't need to hydrate on-demand.
            if (_engineOptions.Value.Enabled)
                await _gameStateManager.GetOrLoadGameAsync(gameId, CancellationToken.None);

            var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId);
            if (publicState is not null)
            {
                await Clients.Caller.SendAsync("TableStateUpdated", publicState);
            }

            var dbPrivateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, privateStateUserId);
            if (dbPrivateState is not null)
            {
                await Clients.Caller.SendAsync("PrivateStateUpdated", dbPrivateState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending state snapshot for game {GameId} to user {UserId}",
                gameId, userId);
            throw new HubException("Failed to retrieve game state");
        }
    }

    /// <summary>
    /// Gets the stable user identifier from the connection context.
    /// Uses email claim to match existing identity usage in the application.
    /// </summary>
    private string? GetUserIdentifier()
    {
        // Match the claim order used by CurrentUserService.UserName
        return Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? Context.User?.FindFirst("email")?.Value
            ?? Context.User?.FindFirst("preferred_username")?.Value
            ?? Context.User?.Identity?.Name;
    }

    /// <summary>
    /// Gets the SignalR group name for a game.
    /// </summary>
    private static string GetGroupName(Guid gameId) => $"{GameGroupPrefix}{gameId}";
}
