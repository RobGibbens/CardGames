using Microsoft.AspNetCore.SignalR.Client;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Service for managing SignalR hub connections in Blazor.
/// Provides a fallback/reconnection mechanism for real-time communication.
/// </summary>
public class GameHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ILogger<GameHubService> _logger;
    private readonly IConfiguration _configuration;
    private bool _isConnecting;

    /// <summary>
    /// Event raised when a message is received from the hub.
    /// </summary>
    public event Action<string, DateTime>? OnMessageReceived;

    /// <summary>
    /// Event raised when the connection state changes.
    /// </summary>
    public event Action<string>? OnConnectionChanged;

    /// <summary>
    /// Event raised when connected to the hub with the connection ID.
    /// </summary>
    public event Action<string>? OnConnected;

    /// <summary>
    /// Event raised when a player joins a game.
    /// </summary>
    public event Action<string, string>? OnPlayerJoined;

    /// <summary>
    /// Event raised when a player leaves a game.
    /// </summary>
    public event Action<string, string>? OnPlayerLeft;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public HubConnectionState State => _hubConnection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Gets whether the connection is established.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public GameHubService(ILogger<GameHubService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Starts the SignalR connection with automatic reconnection support.
    /// </summary>
    /// <param name="hubUrl">The URL of the SignalR hub.</param>
    public async Task StartAsync(string hubUrl)
    {
        if (_isConnecting || IsConnected)
        {
            _logger.LogDebug("Connection already established or in progress");
            return;
        }

        _isConnecting = true;

        try
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new RetryPolicy())
                .Build();

            // Register event handlers
            _hubConnection.On<string, DateTime>("ReceiveMessage", (message, timestamp) =>
            {
                _logger.LogDebug("Received message: {Message} at {Timestamp}", message, timestamp);
                OnMessageReceived?.Invoke(message, timestamp);
            });

            _hubConnection.On<string>("Connected", (connectionId) =>
            {
                _logger.LogInformation("Connected to hub with ID: {ConnectionId}", connectionId);
                OnConnected?.Invoke(connectionId);
            });

            _hubConnection.On<string, string>("PlayerJoined", (connectionId, gameId) =>
            {
                _logger.LogInformation("Player {ConnectionId} joined game {GameId}", connectionId, gameId);
                OnPlayerJoined?.Invoke(connectionId, gameId);
            });

            _hubConnection.On<string, string>("PlayerLeft", (connectionId, gameId) =>
            {
                _logger.LogInformation("Player {ConnectionId} left game {GameId}", connectionId, gameId);
                OnPlayerLeft?.Invoke(connectionId, gameId);
            });

            // Connection state change handlers
            _hubConnection.Reconnecting += error =>
            {
                _logger.LogWarning("Connection lost. Attempting to reconnect... Error: {Error}", error?.Message);
                OnConnectionChanged?.Invoke("Reconnecting");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                _logger.LogInformation("Reconnected to hub with ID: {ConnectionId}", connectionId);
                OnConnectionChanged?.Invoke("Connected");
                return Task.CompletedTask;
            };

            _hubConnection.Closed += error =>
            {
                _logger.LogError("Connection closed. Error: {Error}", error?.Message);
                OnConnectionChanged?.Invoke("Disconnected");
                return Task.CompletedTask;
            };

            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established to {HubUrl}", hubUrl);
            OnConnectionChanged?.Invoke("Connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish SignalR connection to {HubUrl}", hubUrl);
            OnConnectionChanged?.Invoke("Failed");
            throw;
        }
        finally
        {
            _isConnecting = false;
        }
    }

    /// <summary>
    /// Sends a message through the hub to all connected clients.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public async Task SendMessageAsync(string message)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot send message - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("SendMessage", message);
    }

    /// <summary>
    /// Joins a specific game room.
    /// </summary>
    /// <param name="gameId">The game identifier to join.</param>
    public async Task JoinGameAsync(string gameId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join game - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("JoinGame", gameId);
    }

    /// <summary>
    /// Leaves a specific game room.
    /// </summary>
    /// <param name="gameId">The game identifier to leave.</param>
    public async Task LeaveGameAsync(string gameId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot leave game - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("LeaveGame", gameId);
    }

    /// <summary>
    /// Stops the SignalR connection.
    /// </summary>
    public async Task StopAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.StopAsync();
            _logger.LogInformation("SignalR connection stopped");
        }
    }

    /// <summary>
    /// Disposes the hub connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }

    /// <summary>
    /// Custom retry policy for automatic reconnection.
    /// </summary>
    private class RetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] RetryDelays =
        [
            TimeSpan.FromSeconds(0),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        ];

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            if (retryContext.PreviousRetryCount < RetryDelays.Length)
            {
                return RetryDelays[retryContext.PreviousRetryCount];
            }

            // After all delays exhausted, retry every 30 seconds
            return TimeSpan.FromSeconds(30);
        }
    }
}
