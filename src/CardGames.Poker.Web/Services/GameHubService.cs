using Microsoft.AspNetCore.SignalR.Client;
using CardGames.Poker.Shared.Events;

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
    private Timer? _heartbeatTimer;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);

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
    /// Event raised when the seat status of a table changes.
    /// </summary>
    public event Action<TableSeatStatusChangedEvent>? OnTableSeatStatusChanged;

    /// <summary>
    /// Event raised when a seat becomes available at a table.
    /// </summary>
    public event Action<SeatAvailableEvent>? OnSeatAvailable;

    /// <summary>
    /// Event raised when a player joins a waiting list.
    /// </summary>
    public event Action<PlayerJoinedWaitingListEvent>? OnPlayerJoinedWaitingList;

    /// <summary>
    /// Event raised when a player leaves a waiting list.
    /// </summary>
    public event Action<PlayerLeftWaitingListEvent>? OnPlayerLeftWaitingList;

    #region New Game Events

    /// <summary>
    /// Event raised when a player connects to a table.
    /// </summary>
    public event Action<PlayerConnectedEvent>? OnPlayerConnected;

    /// <summary>
    /// Event raised when a player disconnects from a table.
    /// </summary>
    public event Action<PlayerDisconnectedEvent>? OnPlayerDisconnected;

    /// <summary>
    /// Event raised when a player reconnects to a table.
    /// </summary>
    public event Action<PlayerReconnectedEvent>? OnPlayerReconnected;

    /// <summary>
    /// Event raised when a betting action is performed.
    /// </summary>
    public event Action<BettingActionEvent>? OnPlayerAction;

    /// <summary>
    /// Event raised when an action is rejected.
    /// </summary>
    public event Action<string>? OnActionRejected;

    /// <summary>
    /// Event raised when table state synchronization is received.
    /// </summary>
    public event Action<TableStateSyncEvent>? OnTableStateSync;

    /// <summary>
    /// Event raised when private player data is received.
    /// </summary>
    public event Action<PrivatePlayerDataEvent>? OnPrivateData;

    /// <summary>
    /// Event raised when a connection is detected as stale.
    /// </summary>
    public event Action<ConnectionStaleEvent>? OnConnectionStale;

    /// <summary>
    /// Event raised when a heartbeat acknowledgment is received.
    /// </summary>
    public event Action<DateTime>? OnHeartbeatAck;

    /// <summary>
    /// Event raised when a ping request is received from the server.
    /// </summary>
    public event Action<DateTime>? OnPingRequest;

    /// <summary>
    /// Event raised when showdown starts.
    /// </summary>
    public event Action<ShowdownStartedEvent>? OnShowdownStarted;

    /// <summary>
    /// Event raised when a player reveals their cards.
    /// </summary>
    public event Action<PlayerRevealedCardsEvent>? OnPlayerRevealedCards;

    /// <summary>
    /// Event raised when a player mucks their cards.
    /// </summary>
    public event Action<PlayerMuckedCardsEvent>? OnPlayerMuckedCards;

    /// <summary>
    /// Event raised when it's a player's turn at showdown.
    /// </summary>
    public event Action<ShowdownTurnEvent>? OnShowdownTurn;

    /// <summary>
    /// Event raised when showdown is completed.
    /// </summary>
    public event Action<ShowdownCompletedEvent>? OnShowdownCompleted;

    /// <summary>
    /// Event raised when a winner is announced.
    /// </summary>
    public event Action<WinnerAnnouncementEvent>? OnWinnerAnnouncement;

    /// <summary>
    /// Event raised when all-in board run-out starts.
    /// </summary>
    public event Action<AllInBoardRunOutStartedEvent>? OnAllInBoardRunOutStarted;

    /// <summary>
    /// Event raised when a community card is revealed during all-in run-out.
    /// </summary>
    public event Action<AllInBoardCardRevealedEvent>? OnAllInBoardCardRevealed;

    /// <summary>
    /// Event raised when a player's hand is auto-revealed.
    /// </summary>
    public event Action<AutoRevealEvent>? OnAutoReveal;

    /// <summary>
    /// Event raised when showdown animation sequence is ready.
    /// </summary>
    public event Action<ShowdownAnimationReadyEvent>? OnShowdownAnimationReady;

    /// <summary>
    /// Event raised when hand starts.
    /// </summary>
    public event Action<HandStartedEvent>? OnHandStarted;

    /// <summary>
    /// Event raised when community cards are dealt.
    /// </summary>
    public event Action<CommunityCardsDealtEvent>? OnCommunityCardsDealt;

    /// <summary>
    /// Event raised when it's a player's turn.
    /// </summary>
    public event Action<PlayerTurnEvent>? OnPlayerTurn;

    #endregion

    #region Chat Events

    /// <summary>
    /// Event raised when a chat message is received.
    /// </summary>
    public event Action<ChatMessageSentEvent>? OnChatMessageReceived;

    /// <summary>
    /// Event raised when a chat message is rejected.
    /// </summary>
    public event Action<ChatMessageRejectedEvent>? OnChatMessageRejected;

    /// <summary>
    /// Event raised when a system announcement is received.
    /// </summary>
    public event Action<SystemAnnouncementEvent>? OnSystemAnnouncement;

    /// <summary>
    /// Event raised when table chat status changes.
    /// </summary>
    public event Action<TableChatStatusChangedEvent>? OnTableChatStatusChanged;

    /// <summary>
    /// Event raised when a player is muted.
    /// </summary>
    public event Action<PlayerMutedEvent>? OnPlayerMuted;

    /// <summary>
    /// Event raised when a player is unmuted.
    /// </summary>
    public event Action<PlayerUnmutedEvent>? OnPlayerUnmuted;

    #endregion

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public HubConnectionState State => _hubConnection?.State ?? HubConnectionState.Disconnected;

    /// <summary>
    /// Gets whether the connection is established.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Gets the current connection ID if connected.
    /// </summary>
    public string? ConnectionId { get; private set; }

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
                ConnectionId = connectionId;
                _logger.LogInformation("Connected to hub with ID: {ConnectionId}", connectionId);
                StartHeartbeat();
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

            _hubConnection.On<TableSeatStatusChangedEvent>("TableSeatStatusChanged", (seatStatusEvent) =>
            {
                _logger.LogInformation("Table {TableId} seat status changed: {OccupiedSeats}/{MaxSeats}", 
                    seatStatusEvent.TableId, seatStatusEvent.OccupiedSeats, seatStatusEvent.MaxSeats);
                OnTableSeatStatusChanged?.Invoke(seatStatusEvent);
            });

            _hubConnection.On<SeatAvailableEvent>("SeatAvailable", (seatAvailableEvent) =>
            {
                _logger.LogInformation("Seat available at table {TableId}", seatAvailableEvent.TableId);
                OnSeatAvailable?.Invoke(seatAvailableEvent);
            });

            _hubConnection.On<PlayerJoinedWaitingListEvent>("PlayerJoinedWaitingList", (waitingListEvent) =>
            {
                _logger.LogInformation("Player {PlayerName} joined waiting list at table {TableId}", 
                    waitingListEvent.PlayerName, waitingListEvent.TableId);
                OnPlayerJoinedWaitingList?.Invoke(waitingListEvent);
            });

            _hubConnection.On<PlayerLeftWaitingListEvent>("PlayerLeftWaitingList", (waitingListEvent) =>
            {
                _logger.LogInformation("Player {PlayerName} left waiting list at table {TableId}", 
                    waitingListEvent.PlayerName, waitingListEvent.TableId);
                OnPlayerLeftWaitingList?.Invoke(waitingListEvent);
            });

            // New game event handlers
            _hubConnection.On<PlayerConnectedEvent>("PlayerConnected", (evt) =>
            {
                _logger.LogInformation("Player {PlayerName} connected to table {TableId}", evt.PlayerName, evt.TableId);
                OnPlayerConnected?.Invoke(evt);
            });

            _hubConnection.On<PlayerDisconnectedEvent>("PlayerDisconnected", (evt) =>
            {
                _logger.LogInformation("Player {PlayerName} disconnected from table {TableId}", evt.PlayerName, evt.TableId);
                OnPlayerDisconnected?.Invoke(evt);
            });

            _hubConnection.On<PlayerReconnectedEvent>("PlayerReconnected", (evt) =>
            {
                _logger.LogInformation("Player {PlayerName} reconnected to table {TableId}", evt.PlayerName, evt.TableId);
                OnPlayerReconnected?.Invoke(evt);
            });

            _hubConnection.On<BettingActionEvent>("PlayerAction", (evt) =>
            {
                _logger.LogInformation("Player {PlayerName} performed {ActionType}", evt.Action.PlayerName, evt.Action.ActionType);
                OnPlayerAction?.Invoke(evt);
            });

            _hubConnection.On<string>("ActionRejected", (reason) =>
            {
                _logger.LogWarning("Action rejected: {Reason}", reason);
                OnActionRejected?.Invoke(reason);
            });

            _hubConnection.On<TableStateSyncEvent>("TableStateSync", (evt) =>
            {
                _logger.LogDebug("Received table state sync for table {TableId}", evt.TableId);
                OnTableStateSync?.Invoke(evt);
            });

            _hubConnection.On<PrivatePlayerDataEvent>("PrivateData", (evt) =>
            {
                _logger.LogDebug("Received private data for player {PlayerName}", evt.PlayerName);
                OnPrivateData?.Invoke(evt);
            });

            _hubConnection.On<ConnectionStaleEvent>("ConnectionStale", (evt) =>
            {
                _logger.LogWarning("Connection stale detected for player {PlayerName}", evt.PlayerName);
                OnConnectionStale?.Invoke(evt);
            });

            _hubConnection.On<DateTime>("HeartbeatAck", (timestamp) =>
            {
                _logger.LogTrace("Heartbeat acknowledged at {Timestamp}", timestamp);
                OnHeartbeatAck?.Invoke(timestamp);
            });

            _hubConnection.On<DateTime>("PingRequest", (timestamp) =>
            {
                _logger.LogDebug("Ping request received at {Timestamp}", timestamp);
                OnPingRequest?.Invoke(timestamp);
                // Automatically respond with heartbeat
                _ = SendHeartbeatAsync();
            });

            _hubConnection.On<ShowdownStartedEvent>("ShowdownStarted", (evt) =>
            {
                _logger.LogInformation("Showdown started for game {GameId}", evt.GameId);
                OnShowdownStarted?.Invoke(evt);
            });

            _hubConnection.On<PlayerRevealedCardsEvent>("PlayerRevealedCards", (evt) =>
            {
                _logger.LogInformation("Player {PlayerName} revealed cards", evt.PlayerName);
                OnPlayerRevealedCards?.Invoke(evt);
            });

            _hubConnection.On<PlayerMuckedCardsEvent>("PlayerMuckedCards", (evt) =>
            {
                _logger.LogInformation("Player {PlayerName} mucked cards", evt.PlayerName);
                OnPlayerMuckedCards?.Invoke(evt);
            });

            _hubConnection.On<ShowdownTurnEvent>("ShowdownTurn", (evt) =>
            {
                _logger.LogInformation("Showdown turn for player {PlayerName}", evt.PlayerName);
                OnShowdownTurn?.Invoke(evt);
            });

            _hubConnection.On<ShowdownCompletedEvent>("ShowdownCompleted", (evt) =>
            {
                _logger.LogInformation("Showdown completed for game {GameId}", evt.GameId);
                OnShowdownCompleted?.Invoke(evt);
            });

            _hubConnection.On<WinnerAnnouncementEvent>("WinnerAnnouncement", (evt) =>
            {
                _logger.LogInformation("Winner announcement for game {GameId}", evt.GameId);
                OnWinnerAnnouncement?.Invoke(evt);
            });

            _hubConnection.On<AllInBoardRunOutStartedEvent>("AllInBoardRunOutStarted", (evt) =>
            {
                _logger.LogInformation("All-in board run-out started for game {GameId}", evt.GameId);
                OnAllInBoardRunOutStarted?.Invoke(evt);
            });

            _hubConnection.On<AllInBoardCardRevealedEvent>("AllInBoardCardRevealed", (evt) =>
            {
                _logger.LogInformation("All-in board card revealed for game {GameId}: {Card}", evt.GameId, evt.Card.DisplayValue);
                OnAllInBoardCardRevealed?.Invoke(evt);
            });

            _hubConnection.On<AutoRevealEvent>("AutoReveal", (evt) =>
            {
                _logger.LogInformation("Auto-reveal for player {PlayerName} in game {GameId}", evt.PlayerName, evt.GameId);
                OnAutoReveal?.Invoke(evt);
            });

            _hubConnection.On<ShowdownAnimationReadyEvent>("ShowdownAnimationReady", (evt) =>
            {
                _logger.LogInformation("Showdown animation ready for game {GameId}", evt.GameId);
                OnShowdownAnimationReady?.Invoke(evt);
            });

            _hubConnection.On<HandStartedEvent>("HandStarted", (evt) =>
            {
                _logger.LogInformation("Hand {HandNumber} started for game {GameId}", evt.HandNumber, evt.GameId);
                OnHandStarted?.Invoke(evt);
            });

            _hubConnection.On<CommunityCardsDealtEvent>("CommunityCardsDealt", (evt) =>
            {
                _logger.LogInformation("Community cards dealt: {Street}", evt.StreetName);
                OnCommunityCardsDealt?.Invoke(evt);
            });

            _hubConnection.On<PlayerTurnEvent>("PlayerTurn", (evt) =>
            {
                _logger.LogInformation("It's {PlayerName}'s turn", evt.PlayerName);
                OnPlayerTurn?.Invoke(evt);
            });

            // Chat event handlers
            _hubConnection.On<ChatMessageSentEvent>("ChatMessageReceived", (evt) =>
            {
                _logger.LogDebug("Chat message from {SenderName}", evt.Message.SenderName);
                OnChatMessageReceived?.Invoke(evt);
            });

            _hubConnection.On<ChatMessageRejectedEvent>("ChatMessageRejected", (evt) =>
            {
                _logger.LogWarning("Chat message rejected for {PlayerName}: {Reason}", evt.PlayerName, evt.Reason);
                OnChatMessageRejected?.Invoke(evt);
            });

            _hubConnection.On<SystemAnnouncementEvent>("SystemAnnouncement", (evt) =>
            {
                _logger.LogDebug("System announcement: {Content}", evt.Content);
                OnSystemAnnouncement?.Invoke(evt);
            });

            _hubConnection.On<TableChatStatusChangedEvent>("TableChatStatusChanged", (evt) =>
            {
                _logger.LogInformation("Table chat {Status}", evt.IsChatEnabled ? "enabled" : "disabled");
                OnTableChatStatusChanged?.Invoke(evt);
            });

            _hubConnection.On<PlayerMutedEvent>("PlayerMuted", (evt) =>
            {
                _logger.LogDebug("Player {PlayerName} muted {MutedPlayer}", evt.PlayerName, evt.MutedPlayerName);
                OnPlayerMuted?.Invoke(evt);
            });

            _hubConnection.On<PlayerUnmutedEvent>("PlayerUnmuted", (evt) =>
            {
                _logger.LogDebug("Player {PlayerName} unmuted {UnmutedPlayer}", evt.PlayerName, evt.UnmutedPlayerName);
                OnPlayerUnmuted?.Invoke(evt);
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
    /// Joins the lobby group to receive table updates.
    /// </summary>
    public async Task JoinLobbyAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join lobby - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("JoinLobby");
    }

    /// <summary>
    /// Leaves the lobby group.
    /// </summary>
    public async Task LeaveLobbyAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot leave lobby - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("LeaveLobby");
    }

    /// <summary>
    /// Joins a table's waiting list group to receive seat notifications.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task JoinWaitingListGroupAsync(string tableId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join waiting list group - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("JoinWaitingList", tableId);
    }

    /// <summary>
    /// Leaves a table's waiting list group.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task LeaveWaitingListGroupAsync(string tableId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot leave waiting list group - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("LeaveWaitingListGroup", tableId);
    }

    #region Table Actions

    /// <summary>
    /// Joins a table as a seated player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The player's name.</param>
    public async Task JoinTableAsync(string tableId, string playerName)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot join table - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("JoinTable", tableId, playerName);
    }

    /// <summary>
    /// Leaves a table as a seated player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task LeaveTableAsync(string tableId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot leave table - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("LeaveTable", tableId);
    }

    /// <summary>
    /// Performs a fold action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task FoldAsync(string tableId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot fold - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("Fold", tableId);
    }

    /// <summary>
    /// Performs a check action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task CheckAsync(string tableId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot check - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("Check", tableId);
    }

    /// <summary>
    /// Performs a call action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The amount to call.</param>
    public async Task CallAsync(string tableId, int amount)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot call - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("Call", tableId, amount);
    }

    /// <summary>
    /// Performs a bet action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The amount to bet.</param>
    public async Task BetAsync(string tableId, int amount)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot bet - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("Bet", tableId, amount);
    }

    /// <summary>
    /// Performs a raise action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The total amount to raise to.</param>
    public async Task RaiseAsync(string tableId, int amount)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot raise - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("Raise", tableId, amount);
    }

    /// <summary>
    /// Performs an all-in action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The player's remaining chip stack.</param>
    public async Task AllInAsync(string tableId, int amount)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot go all-in - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("AllIn", tableId, amount);
    }

    #endregion

    #region Table State Synchronization

    /// <summary>
    /// Requests full table state synchronization.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task RequestTableStateAsync(string tableId)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot request table state - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("RequestTableState", tableId);
    }

    #endregion

    #region Connection Health

    /// <summary>
    /// Sends a heartbeat to the server to keep the connection alive.
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            return;
        }

        try
        {
            await _hubConnection.InvokeAsync("Heartbeat");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send heartbeat");
        }
    }

    /// <summary>
    /// Gets connection information from the server.
    /// </summary>
    public async Task GetConnectionInfoAsync()
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot get connection info - not connected to hub");
            return;
        }

        await _hubConnection.InvokeAsync("GetConnectionInfo");
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(
            _ => 
            {
                // Fire-and-forget but safely handle exceptions
                _ = SendHeartbeatSafeAsync();
            },
            null,
            _heartbeatInterval,
            _heartbeatInterval);
        _logger.LogDebug("Heartbeat timer started with interval {Interval}", _heartbeatInterval);
    }

    private async Task SendHeartbeatSafeAsync()
    {
        try
        {
            await SendHeartbeatAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Heartbeat failed - connection may be closing");
        }
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _logger.LogDebug("Heartbeat timer stopped");
    }

    #endregion

    /// <summary>
    /// Stops the SignalR connection.
    /// </summary>
    public async Task StopAsync()
    {
        StopHeartbeat();
        ConnectionId = null;
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
        StopHeartbeat();
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
