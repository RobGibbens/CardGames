using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Scoped service for managing SignalR hub connections to the lobby.
/// Each Blazor circuit gets its own instance.
/// </summary>
public sealed class LobbyHubClient : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly InternalApiUserTokenFactory _internalApiUserTokenFactory;
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
        InternalApiUserTokenFactory internalApiUserTokenFactory,
        ILogger<LobbyHubClient> logger)
    {
        _configuration = configuration;
        _authStateProvider = authStateProvider;
        _internalApiUserTokenFactory = internalApiUserTokenFactory;
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

        var hubUrl = GetHubUrl();
        _logger.LogInformation("Connecting to lobby hub at {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => GetAccessTokenAsync();
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

    private string GetHubUrl()
    {
        // Use service discovery URL pattern matching the API client configuration
        var baseUrl = _configuration["Services:Api:Https:0"]
            ?? _configuration["Services:Api:Http:0"]
            ?? "https://localhost:7001";

        return $"{baseUrl}/hubs/lobby";
    }

    private async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            return _internalApiUserTokenFactory.CreateToken(authState.User);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create internal hub token");
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
