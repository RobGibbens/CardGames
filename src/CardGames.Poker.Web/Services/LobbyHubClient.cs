using CardGames.Contracts.SignalR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using System.Security.Claims;
using System.Web;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Scoped service for managing SignalR hub connections to the lobby.
/// Each Blazor circuit gets its own instance.
/// </summary>
public sealed class LobbyHubClient : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<LobbyHubClient> _logger;

    private HubConnection? _hubConnection;
    private bool _isInLobby;

    /// <summary>
    /// Fired when a new game is created.
    /// </summary>
    public event Func<GameCreatedDto, Task>? OnGameCreated;

    /// <summary>
    /// Fired when a game is deleted or ended.
    /// </summary>
    public event Func<Guid, Task>? OnGameDeleted;

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
    /// Gets whether the client is in the lobby group.
    /// </summary>
    public bool IsInLobby => _isInLobby && IsConnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyHubClient"/> class.
    /// </summary>
    public LobbyHubClient(
        IConfiguration configuration,
        AuthenticationStateProvider authStateProvider,
        ILogger<LobbyHubClient> logger)
    {
        _configuration = configuration;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Connects to the lobby hub.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is not null)
        {
            _logger.LogDebug("Lobby hub connection already exists, state: {State}", _hubConnection.State);
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                return;
            }
        }

        // Get user info for authentication
        var userInfo = await GetUserInfoAsync();
        var hubUrl = GetHubUrl(userInfo);
        _logger.LogInformation("Connecting to lobby hub at {HubUrl}", hubUrl);

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
                }
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        // Wire up connection lifecycle events
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;
        _hubConnection.Closed += OnClosed;

        // Subscribe to hub messages
        _hubConnection.On<GameCreatedDto>("GameCreated", HandleGameCreated);
        _hubConnection.On<Guid>("GameDeleted", HandleGameDeleted);

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            _logger.LogInformation("Connected to lobby hub");
            OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to lobby hub");
            throw;
        }
    }

    /// <summary>
    /// Joins the lobby group to receive game updates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task JoinLobbyAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join lobby - not connected to hub");
            throw new InvalidOperationException("Not connected to lobby hub");
        }

        if (_isInLobby)
        {
            _logger.LogDebug("Already in lobby group");
            return;
        }

        _logger.LogInformation("Joining lobby group");
        await _hubConnection.InvokeAsync("JoinLobby", cancellationToken);
        _isInLobby = true;
        _logger.LogInformation("Joined lobby group");
    }

    /// <summary>
    /// Leaves the lobby group.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LeaveLobbyAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null || _hubConnection.State != HubConnectionState.Connected)
        {
            _logger.LogDebug("Cannot leave lobby - not connected to hub");
            _isInLobby = false;
            return;
        }

        if (!_isInLobby)
        {
            _logger.LogDebug("Not in lobby group");
            return;
        }

        _logger.LogInformation("Leaving lobby group");
        try
        {
            await _hubConnection.InvokeAsync("LeaveLobby", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leaving lobby group");
        }

        _isInLobby = false;
    }

    /// <summary>
    /// Disconnects from the lobby hub.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
        {
            return;
        }

        _logger.LogInformation("Disconnecting from lobby hub");

        // Leave lobby first
        if (_isInLobby)
        {
            await LeaveLobbyAsync(cancellationToken);
        }

        try
        {
            await _hubConnection.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping lobby hub connection");
        }

        OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
    }

    private string GetHubUrl((string? UserId, string? UserName)? userInfo)
    {
        // Use service discovery URL pattern matching the API client configuration
        var baseUrl = _configuration["Services:Api:Https:0"]
            ?? _configuration["Services:Api:Http:0"]
            ?? "https://localhost:7001";

        var url = $"{baseUrl}/hubs/lobby";

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
            url = $"{url}?{string.Join("&", queryParams)}";
        }

        return url;
    }

    private async Task<(string? UserId, string? UserName)?> GetUserInfoAsync()
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

            var userName = user.FindFirstValue(ClaimTypes.Email)
                           ?? user.FindFirstValue("email")
                           ?? user.FindFirstValue("preferred_username")
                           ?? user.Identity?.Name;

            return (userId, userName);
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
            _logger.LogWarning(exception, "Lobby hub connection lost, attempting to reconnect...");
        }
        else
        {
            _logger.LogInformation("Lobby hub connection reconnecting...");
        }

        _isInLobby = false;
        OnConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("Lobby hub connection restored with connection ID {ConnectionId}", connectionId);
        OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);

        // Rejoin the lobby after reconnection
        _logger.LogInformation("Rejoining lobby after reconnection");
        try
        {
            await JoinLobbyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rejoin lobby after reconnection");
        }
    }

    private Task OnClosed(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogError(exception, "Lobby hub connection closed with error");
        }
        else
        {
            _logger.LogInformation("Lobby hub connection closed");
        }

        _isInLobby = false;
        OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private async Task HandleGameCreated(GameCreatedDto gameCreated)
    {
        _logger.LogDebug("Received GameCreated for game {GameId} ({GameName})", 
            gameCreated.GameId, gameCreated.Name);

        if (OnGameCreated is not null)
        {
            await OnGameCreated(gameCreated);
        }
    }

    private async Task HandleGameDeleted(Guid gameId)
    {
        _logger.LogDebug("Received GameDeleted for game {GameId}", gameId);

        if (OnGameDeleted is not null)
        {
            await OnGameDeleted(gameId);
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
