using CardGames.Contracts.SignalR;
using CardGames.Contracts.TableSettings;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Web;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Scoped service for managing SignalR hub connections to the game server.
/// Each Blazor circuit gets its own instance.
/// </summary>
public sealed class GameHubClient : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<GameHubClient> _logger;

    private HubConnection? _hubConnection;
    private Guid? _currentGameId;

    /// <summary>
    /// Fired when the public table state is updated.
    /// </summary>
    public event Func<TableStatePublicDto, Task>? OnTableStateUpdated;

    /// <summary>
    /// Fired when the private state for the current player is updated.
    /// </summary>
    public event Func<PrivateStateDto, Task>? OnPrivateStateUpdated;

    /// <summary>
    /// Fired when a player joins the game.
    /// </summary>
    public event Func<PlayerJoinedDto, Task>? OnPlayerJoined;

    /// <summary>
    /// Fired when table settings are updated.
    /// </summary>
    public event Func<TableSettingsUpdatedDto, Task>? OnTableSettingsUpdated;

    /// <summary>
    /// Fired when the action timer is updated.
    /// </summary>
    public event Func<ActionTimerStateDto, Task>? OnActionTimerUpdated;

    /// <summary>
    /// Fired when the connection state changes.
    /// </summary>
    public event Action<HubConnectionState>? OnConnectionStateChanged;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Gets whether the client is connected.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Gets whether the client is reconnecting.
    /// </summary>
    public bool IsReconnecting => _hubConnection?.State == HubConnectionState.Reconnecting;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHubClient"/> class.
    /// </summary>
    public GameHubClient(
        IConfiguration configuration,
        AuthenticationStateProvider authStateProvider,
        ILogger<GameHubClient> logger)
    {
        _configuration = configuration;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Connects to the game hub.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("Hub connection already exists, state: {State}", _hubConnection.State);
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                return;
            }
        }

        // Get user info for authentication
        var userInfo = await GetUserInfoAsync();
        var hubUrl = GetHubUrl(userInfo);
        _logger.LogInformation("Connecting to game hub at {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Add user identity headers for authentication
                if (userInfo is not null)
                {
                    options.Headers["X-User-Authenticated"] = "true";
                    if (!string.IsNullOrEmpty(userInfo.Value.UserId))
                    {
                        options.Headers["X-User-Id"] = userInfo.Value.UserId;
                    }
                    if (!string.IsNullOrEmpty(userInfo.Value.UserName))
                    {
                        options.Headers["X-User-Name"] = userInfo.Value.UserName;
                    }
                    if (!string.IsNullOrEmpty(userInfo.Value.UserEmail))
                    {
                        options.Headers["X-User-Email"] = userInfo.Value.UserEmail;
                    }
                }
            })
            .AddJsonProtocol(options =>
            {
                // Match server-side JSON settings for proper deserialization
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        // Wire up connection lifecycle events
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;
        _hubConnection.Closed += OnClosed;

        // Subscribe to hub messages
        _hubConnection.On<TableStatePublicDto>("TableStateUpdated", HandleTableStateUpdated);
        _hubConnection.On<PrivateStateDto>("PrivateStateUpdated", HandlePrivateStateUpdated);
        _hubConnection.On<PlayerJoinedDto>("PlayerJoined", HandlePlayerJoined);
        _hubConnection.On<TableSettingsUpdatedDto>("TableSettingsUpdated", HandleTableSettingsUpdated);
        _hubConnection.On<ActionTimerStateDto>("ActionTimerUpdated", HandleActionTimerUpdated);

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            _logger.LogInformation("Connected to game hub");
            OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to game hub");
            throw;
        }
    }

    /// <summary>
    /// Joins a game group to receive state updates.
    /// </summary>
    /// <param name="gameId">The game ID to join.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task JoinGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join game - not connected to hub");
            throw new InvalidOperationException("Not connected to game hub");
        }

        // Leave current game if different
        if (_currentGameId.HasValue && _currentGameId.Value != gameId)
        {
            await LeaveGameAsync(_currentGameId.Value, cancellationToken);
        }

        _logger.LogInformation("Joining game {GameId}", gameId);
        await _hubConnection.InvokeAsync("JoinGame", gameId, cancellationToken);
        _currentGameId = gameId;
        _logger.LogInformation("Joined game {GameId}", gameId);
    }

    /// <summary>
    /// Leaves a game group.
    /// </summary>
    /// <param name="gameId">The game ID to leave.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LeaveGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            _logger.LogDebug("Cannot leave game - not connected to hub");
            return;
        }

        _logger.LogInformation("Leaving game {GameId}", gameId);
        try
        {
            await _hubConnection.InvokeAsync("LeaveGame", gameId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leaving game {GameId}", gameId);
        }

        if (_currentGameId == gameId)
        {
            _currentGameId = null;
        }
    }

    /// <summary>
    /// Disconnects from the game hub.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
        {
            return;
        }

        _logger.LogInformation("Disconnecting from game hub");

        // Leave current game first
        if (_currentGameId.HasValue)
        {
            await LeaveGameAsync(_currentGameId.Value, cancellationToken);
        }

        try
        {
            await _hubConnection.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping hub connection");
        }

        OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
    }

    private string GetHubUrl((string? UserId, string? UserName, string? UserEmail)? userInfo)
    {
        // Use service discovery URL pattern matching the API client configuration
        // The API is accessible at "https+http://api" via Aspire service discovery
        var baseUrl = _configuration["Services:Api:Https:0"]
            ?? _configuration["Services:Api:Http:0"]
            ?? "https://localhost:7001";

        var url = $"{baseUrl}/hubs/game";

        // Add user identity as query parameters for WebSocket upgrade request
        // (headers are not reliably sent during WebSocket handshake)
        if (userInfo is not null)
        {
            var queryParams = new List<string> { "authenticated=true" };
            if (!string.IsNullOrEmpty(userInfo.Value.UserId))
            {
                queryParams.Add($"userId={HttpUtility.UrlEncode(userInfo.Value.UserId)}");
            }
            if (!string.IsNullOrEmpty(userInfo.Value.UserName))
            {
                queryParams.Add($"userName={HttpUtility.UrlEncode(userInfo.Value.UserName)}");
            }
            if (!string.IsNullOrEmpty(userInfo.Value.UserEmail))
            {
                queryParams.Add($"userEmail={HttpUtility.UrlEncode(userInfo.Value.UserEmail)}");
            }
            url = $"{url}?{string.Join("&", queryParams)}";
        }

        return url;
    }

    private async Task<(string? UserId, string? UserName, string? UserEmail)?> GetUserInfoAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue("sub");

            var userEmail = user.FindFirstValue(ClaimTypes.Email)
                         ?? user.FindFirstValue("email");

            var userName = userEmail
                           ?? user.FindFirstValue("preferred_username")
                           ?? user.Identity?.Name;

            // If the IdP doesn't expose an email claim, fall back to treating the username
            // as an email if it looks like one. This enables server-side SignalR routing
            // via IUserIdProvider when the database uses email/name as the stable key.
            userEmail ??= (!string.IsNullOrWhiteSpace(userName) && userName.Contains('@'))
                ? userName
                : null;

            return (userId, userName, userEmail);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user info for SignalR");
            return null;
        }
    }

    private Task OnReconnecting(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "Hub connection lost, attempting to reconnect...");
        }
        else
        {
            _logger.LogInformation("Hub connection reconnecting...");
        }

        OnConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("Hub connection restored with connection ID {ConnectionId}", connectionId);
        OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);

        // Rejoin the current game after reconnection
        if (_currentGameId.HasValue)
        {
            _logger.LogInformation("Rejoining game {GameId} after reconnection", _currentGameId.Value);
            try
            {
                await JoinGameAsync(_currentGameId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rejoin game {GameId} after reconnection", _currentGameId.Value);
            }
        }
    }

    private Task OnClosed(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogError(exception, "Hub connection closed with error");
        }
        else
        {
            _logger.LogInformation("Hub connection closed");
        }

        OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private async Task HandleTableStateUpdated(TableStatePublicDto state)
    {
        _logger.LogDebug("Received TableStateUpdated for game {GameId}", state.GameId);

        if (OnTableStateUpdated is not null)
        {
            await OnTableStateUpdated(state);
        }
    }

    private async Task HandlePrivateStateUpdated(PrivateStateDto state)
    {
        _logger.LogDebug("Received PrivateStateUpdated for game {GameId}, player {PlayerName}",
            state.GameId, state.PlayerName);

        if (OnPrivateStateUpdated is not null)
        {
            await OnPrivateStateUpdated(state);
        }
    }

    private async Task HandlePlayerJoined(PlayerJoinedDto notification)
    {
        _logger.LogDebug("Received PlayerJoined for game {GameId}, player {PlayerName} at seat {SeatIndex}",
            notification.GameId, notification.PlayerName, notification.SeatIndex);

        if (OnPlayerJoined is not null)
        {
            await OnPlayerJoined(notification);
        }
    }

    private async Task HandleTableSettingsUpdated(TableSettingsUpdatedDto notification)
    {
        _logger.LogDebug("Received TableSettingsUpdated for game {GameId}, updated by {UpdatedByName}",
            notification.GameId, notification.UpdatedByName);

        if (OnTableSettingsUpdated is not null)
        {
            await OnTableSettingsUpdated(notification);
        }
    }

    private async Task HandleActionTimerUpdated(ActionTimerStateDto timerState)
    {
        _logger.LogDebug("Received ActionTimerUpdated: {SecondsRemaining}s remaining, seat {SeatIndex}, active: {IsActive}",
            timerState.SecondsRemaining, timerState.PlayerSeatIndex, timerState.IsActive);

        if (OnActionTimerUpdated is not null)
        {
            await OnActionTimerUpdated(timerState);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            _hubConnection.Reconnecting -= OnReconnecting;
            _hubConnection.Reconnected -= OnReconnected;
            _hubConnection.Closed -= OnClosed;

            await DisconnectAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
