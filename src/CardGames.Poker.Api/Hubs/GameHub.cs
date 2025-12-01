using CardGames.Poker.Api.Features.Showdown;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time poker game communication.
/// </summary>
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;
    private readonly IShowdownAuditLogger _showdownAuditLogger;
    private readonly IConnectionMappingService _connectionMapping;

    public GameHub(
        ILogger<GameHub> logger,
        IShowdownAuditLogger showdownAuditLogger,
        IConnectionMappingService connectionMapping)
    {
        _logger = logger;
        _showdownAuditLogger = showdownAuditLogger;
        _connectionMapping = connectionMapping;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);

        if (playerInfo is not null)
        {
            _connectionMapping.MarkDisconnected(connectionId);

            var evt = new PlayerDisconnectedEvent(
                Guid.Parse(playerInfo.TableId),
                DateTime.UtcNow,
                playerInfo.PlayerName,
                exception is not null);

            await Clients.Group(playerInfo.TableId).SendAsync("PlayerDisconnected", evt);

            _logger.LogInformation(
                "Player {PlayerName} disconnected from table {TableId}. Unexpected: {Unexpected}",
                playerInfo.PlayerName,
                playerInfo.TableId,
                exception is not null);
        }

        _logger.LogInformation("Client disconnected: {ConnectionId}", connectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    public async Task SendMessage(string message)
    {
        _logger.LogInformation("Broadcasting message from {ConnectionId}: {Message}", Context.ConnectionId, message);
        await Clients.All.SendAsync("ReceiveMessage", message, DateTime.UtcNow);
    }

    /// <summary>
    /// Joins a specific game room/group.
    /// </summary>
    /// <param name="gameId">The game identifier to join.</param>
    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        _logger.LogInformation("Client {ConnectionId} joined game {GameId}", Context.ConnectionId, gameId);
        await Clients.Group(gameId).SendAsync("PlayerJoined", Context.ConnectionId, gameId);
    }

    /// <summary>
    /// Leaves a specific game room/group.
    /// </summary>
    /// <param name="gameId">The game identifier to leave.</param>
    public async Task LeaveGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
        _logger.LogInformation("Client {ConnectionId} left game {GameId}", Context.ConnectionId, gameId);
        await Clients.Group(gameId).SendAsync("PlayerLeft", Context.ConnectionId, gameId);
    }

    /// <summary>
    /// Joins the lobby group to receive table updates.
    /// </summary>
    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "lobby");
        _logger.LogInformation("Client {ConnectionId} joined lobby", Context.ConnectionId);
        await Clients.Caller.SendAsync("JoinedLobby", Context.ConnectionId);
    }

    /// <summary>
    /// Leaves the lobby group.
    /// </summary>
    public async Task LeaveLobby()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "lobby");
        _logger.LogInformation("Client {ConnectionId} left lobby", Context.ConnectionId);
    }

    /// <summary>
    /// Joins a table's waiting list group to receive seat notifications.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task JoinWaitingList(string tableId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{tableId}-waitlist");
        _logger.LogInformation("Client {ConnectionId} joined waiting list for table {TableId}", Context.ConnectionId, tableId);
        await Clients.Caller.SendAsync("JoinedWaitingList", tableId);
    }

    /// <summary>
    /// Leaves a table's waiting list group.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task LeaveWaitingListGroup(string tableId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{tableId}-waitlist");
        _logger.LogInformation("Client {ConnectionId} left waiting list for table {TableId}", Context.ConnectionId, tableId);
    }

    #region Showdown Events

    /// <summary>
    /// Notifies the game group that showdown has started.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The showdown started event.</param>
    public async Task NotifyShowdownStarted(string gameId, ShowdownStartedEvent evt)
    {
        _logger.LogInformation("Showdown started for game {GameId}, hand {HandNumber}", gameId, evt.HandNumber);
        await Clients.Group(gameId).SendAsync("ShowdownStarted", evt);
        await _showdownAuditLogger.LogShowdownStartedAsync(evt);
    }

    /// <summary>
    /// Notifies the game group that a player revealed their cards.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The player revealed event.</param>
    public async Task NotifyPlayerRevealed(string gameId, PlayerRevealedCardsEvent evt)
    {
        _logger.LogInformation("Player {PlayerName} revealed cards in game {GameId}", evt.PlayerName, gameId);
        await Clients.Group(gameId).SendAsync("PlayerRevealedCards", evt);
        await _showdownAuditLogger.LogPlayerRevealedAsync(evt);
    }

    /// <summary>
    /// Notifies the game group that a player mucked their cards.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The player mucked event.</param>
    public async Task NotifyPlayerMucked(string gameId, PlayerMuckedCardsEvent evt)
    {
        _logger.LogInformation("Player {PlayerName} mucked cards in game {GameId}", evt.PlayerName, gameId);
        await Clients.Group(gameId).SendAsync("PlayerMuckedCards", evt);
        await _showdownAuditLogger.LogPlayerMuckedAsync(evt);
    }

    /// <summary>
    /// Notifies the game group whose turn it is at showdown.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The showdown turn event.</param>
    public async Task NotifyShowdownTurn(string gameId, ShowdownTurnEvent evt)
    {
        _logger.LogInformation("Showdown turn for player {PlayerName} in game {GameId}", evt.PlayerName, gameId);
        await Clients.Group(gameId).SendAsync("ShowdownTurn", evt);
    }

    /// <summary>
    /// Notifies the game group that the showdown is complete.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The showdown completed event.</param>
    public async Task NotifyShowdownCompleted(string gameId, ShowdownCompletedEvent evt)
    {
        _logger.LogInformation("Showdown completed for game {GameId}, hand {HandNumber}. Winners: {Winners}",
            gameId, evt.HandNumber, string.Join(", ", evt.Winners));
        await Clients.Group(gameId).SendAsync("ShowdownCompleted", evt);
        await _showdownAuditLogger.LogShowdownCompletedAsync(evt);
    }

    /// <summary>
    /// Sends private hole cards to a specific player during showdown.
    /// This is used when only certain players should see certain cards.
    /// </summary>
    /// <param name="connectionId">The connection ID of the player.</param>
    /// <param name="playerName">The name of the player whose cards are being sent.</param>
    /// <param name="cards">The hole cards.</param>
    public async Task SendPrivateHoleCards(string connectionId, string playerName, IReadOnlyList<CardDto> cards)
    {
        _logger.LogDebug("Sending private hole cards to player {PlayerName}", playerName);
        await Clients.Client(connectionId).SendAsync("PrivateHoleCards", playerName, cards);
    }

    #endregion

    #region Table Actions

    /// <summary>
    /// Joins a table as a seated player with connection tracking.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The player's name.</param>
    public async Task JoinTable(string tableId, string playerName)
    {
        var connectionId = Context.ConnectionId;

        // Check for reconnection
        var oldInfo = _connectionMapping.TryReconnect(connectionId, playerName, tableId);
        if (oldInfo is not null)
        {
            await Groups.AddToGroupAsync(connectionId, tableId);

            var reconnectDuration = DateTime.UtcNow - (oldInfo.DisconnectedAt ?? DateTime.UtcNow);
            var evt = new PlayerReconnectedEvent(
                Guid.Parse(tableId),
                DateTime.UtcNow,
                playerName,
                reconnectDuration);

            await Clients.Group(tableId).SendAsync("PlayerReconnected", evt);

            _logger.LogInformation(
                "Player {PlayerName} reconnected to table {TableId} after {Duration}",
                playerName, tableId, reconnectDuration);

            return;
        }

        // New connection
        _connectionMapping.AddConnection(connectionId, playerName, tableId);
        await Groups.AddToGroupAsync(connectionId, tableId);

        var connectedEvent = new PlayerConnectedEvent(
            Guid.Parse(tableId),
            DateTime.UtcNow,
            playerName,
            connectionId);

        await Clients.Group(tableId).SendAsync("PlayerConnected", connectedEvent);

        _logger.LogInformation(
            "Player {PlayerName} joined table {TableId} with connection {ConnectionId}",
            playerName, tableId, connectionId);
    }

    /// <summary>
    /// Leaves a table as a seated player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task LeaveTable(string tableId)
    {
        var connectionId = Context.ConnectionId;
        var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);

        if (playerInfo is not null && playerInfo.TableId == tableId)
        {
            _connectionMapping.RemoveConnection(connectionId);
            await Groups.RemoveFromGroupAsync(connectionId, tableId);

            var evt = new PlayerDisconnectedEvent(
                Guid.Parse(tableId),
                DateTime.UtcNow,
                playerInfo.PlayerName,
                false);

            await Clients.Group(tableId).SendAsync("PlayerDisconnected", evt);

            _logger.LogInformation(
                "Player {PlayerName} left table {TableId}",
                playerInfo.PlayerName, tableId);
        }
    }

    /// <summary>
    /// Performs a fold action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task Fold(string tableId)
    {
        await ProcessPlayerAction(tableId, BettingActionType.Fold, 0);
    }

    /// <summary>
    /// Performs a check action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task Check(string tableId)
    {
        await ProcessPlayerAction(tableId, BettingActionType.Check, 0);
    }

    /// <summary>
    /// Performs a call action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The amount to call.</param>
    public async Task Call(string tableId, int amount)
    {
        await ProcessPlayerAction(tableId, BettingActionType.Call, amount);
    }

    /// <summary>
    /// Performs a bet action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The amount to bet.</param>
    public async Task Bet(string tableId, int amount)
    {
        await ProcessPlayerAction(tableId, BettingActionType.Bet, amount);
    }

    /// <summary>
    /// Performs a raise action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The total amount to raise to.</param>
    public async Task Raise(string tableId, int amount)
    {
        await ProcessPlayerAction(tableId, BettingActionType.Raise, amount);
    }

    /// <summary>
    /// Performs an all-in action.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="amount">The player's remaining chip stack.</param>
    public async Task AllIn(string tableId, int amount)
    {
        await ProcessPlayerAction(tableId, BettingActionType.AllIn, amount);
    }

    private async Task ProcessPlayerAction(string tableId, BettingActionType actionType, int amount)
    {
        var connectionId = Context.ConnectionId;
        var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);

        if (playerInfo is null || playerInfo.TableId != tableId)
        {
            _logger.LogWarning(
                "Player action rejected: Connection {ConnectionId} is not registered at table {TableId}",
                connectionId, tableId);
            await Clients.Caller.SendAsync("ActionRejected", "You are not seated at this table.");
            return;
        }

        _connectionMapping.UpdateLastActivity(connectionId);

        var action = new BettingActionDto(
            playerInfo.PlayerName,
            actionType,
            amount,
            DateTime.UtcNow);

        var evt = new BettingActionEvent(
            Guid.Parse(tableId),
            DateTime.UtcNow,
            action,
            0); // Pot after action would be calculated by game logic

        await Clients.Group(tableId).SendAsync("PlayerAction", evt);

        _logger.LogInformation(
            "Player {PlayerName} performed {ActionType} for {Amount} at table {TableId}",
            playerInfo.PlayerName, actionType, amount, tableId);
    }

    #endregion

    #region Table State Synchronization

    /// <summary>
    /// Requests full table state synchronization.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task RequestTableState(string tableId)
    {
        var connectionId = Context.ConnectionId;
        var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);

        _logger.LogInformation(
            "Table state requested by {ConnectionId} for table {TableId}",
            connectionId, tableId);

        // The actual state would be fetched from a game service
        // For now, we send a request event that the server-side game logic can handle
        await Clients.Caller.SendAsync("TableStateRequested", tableId);
    }

    /// <summary>
    /// Sends table state to a specific player (used for sync on reconnect).
    /// </summary>
    /// <param name="connectionId">The target connection ID.</param>
    /// <param name="state">The table state snapshot.</param>
    public async Task SendTableState(string connectionId, TableStateSyncEvent state)
    {
        await Clients.Client(connectionId).SendAsync("TableStateSync", state);
        _logger.LogDebug("Sent table state to connection {ConnectionId}", connectionId);
    }

    /// <summary>
    /// Broadcasts table state to all players at a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="state">The table state snapshot.</param>
    public async Task BroadcastTableState(string tableId, TableStateSyncEvent state)
    {
        await Clients.Group(tableId).SendAsync("TableStateSync", state);
        _logger.LogDebug("Broadcast table state to table {TableId}", tableId);
    }

    #endregion

    #region Private Message Routing

    /// <summary>
    /// Sends private hole cards to a specific player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The target player name.</param>
    /// <param name="holeCards">The hole cards (as display strings).</param>
    /// <param name="handDescription">Optional description of the hand.</param>
    public async Task SendPrivateData(string tableId, string playerName, IReadOnlyList<string> holeCards, string? handDescription = null)
    {
        var connectionId = _connectionMapping.GetConnectionId(playerName, tableId);
        if (connectionId is not null)
        {
            var evt = new PrivatePlayerDataEvent(
                Guid.Parse(tableId),
                DateTime.UtcNow,
                playerName,
                holeCards,
                handDescription);

            await Clients.Client(connectionId).SendAsync("PrivateData", evt);
            _logger.LogDebug("Sent private data to player {PlayerName} at table {TableId}", playerName, tableId);
        }
    }

    /// <summary>
    /// Broadcasts a public message to all players at a table except one.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="excludePlayerName">The player name to exclude.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="data">The event data.</param>
    public async Task BroadcastExceptPlayer(string tableId, string excludePlayerName, string eventName, object data)
    {
        var excludeConnectionId = _connectionMapping.GetConnectionId(excludePlayerName, tableId);
        if (excludeConnectionId is not null)
        {
            await Clients.GroupExcept(tableId, excludeConnectionId).SendAsync(eventName, data);
        }
        else
        {
            await Clients.Group(tableId).SendAsync(eventName, data);
        }
    }

    #endregion

    #region Connection Health

    /// <summary>
    /// Handles heartbeat/ping from client to keep connection alive.
    /// </summary>
    public async Task Heartbeat()
    {
        var connectionId = Context.ConnectionId;
        _connectionMapping.UpdateLastActivity(connectionId);

        await Clients.Caller.SendAsync("HeartbeatAck", DateTime.UtcNow);
        _logger.LogTrace("Heartbeat received from {ConnectionId}", connectionId);
    }

    /// <summary>
    /// Gets connection information for the current connection.
    /// </summary>
    public async Task GetConnectionInfo()
    {
        var connectionId = Context.ConnectionId;
        var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);

        await Clients.Caller.SendAsync("ConnectionInfo", new
        {
            ConnectionId = connectionId,
            PlayerName = playerInfo?.PlayerName,
            TableId = playerInfo?.TableId,
            IsConnected = playerInfo is not null,
            ConnectedAt = playerInfo?.ConnectedAt,
            LastActivity = playerInfo?.LastActivity
        });
    }

    #endregion

    #region Dealer Button and Blinds Events

    /// <summary>
    /// Notifies the game group that the dealer button has moved.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The dealer button moved event.</param>
    public async Task NotifyDealerButtonMoved(string gameId, DealerButtonMovedEvent evt)
    {
        _logger.LogInformation(
            "Dealer button moved in game {GameId} from position {PreviousPosition} to {NewPosition}",
            gameId, evt.PreviousPosition, evt.NewPosition);
        await Clients.Group(gameId).SendAsync("DealerButtonMoved", evt);
    }

    /// <summary>
    /// Notifies the game group that a blind has been posted.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The blind posted event.</param>
    public async Task NotifyBlindPosted(string gameId, BlindPostedEvent evt)
    {
        _logger.LogInformation(
            "Player {PlayerName} posted {BlindType} of {Amount} in game {GameId}",
            evt.PlayerName, evt.BlindType, evt.Amount, gameId);
        await Clients.Group(gameId).SendAsync("BlindPosted", evt);
    }

    /// <summary>
    /// Notifies the game group that an ante has been posted.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The ante posted event.</param>
    public async Task NotifyAntePosted(string gameId, AntePostedEvent evt)
    {
        _logger.LogInformation(
            "Player {PlayerName} posted ante of {Amount} in game {GameId}",
            evt.PlayerName, evt.Amount, gameId);
        await Clients.Group(gameId).SendAsync("AntePosted", evt);
    }

    /// <summary>
    /// Notifies the game group that all antes have been collected.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The antes collected event.</param>
    public async Task NotifyAntesCollected(string gameId, AntesCollectedEvent evt)
    {
        _logger.LogInformation(
            "Antes collected in game {GameId}. Total: {Total}",
            gameId, evt.TotalCollected);
        await Clients.Group(gameId).SendAsync("AntesCollected", evt);
    }

    /// <summary>
    /// Notifies the game group that missed blinds have been recorded for a player.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The missed blind recorded event.</param>
    public async Task NotifyMissedBlindRecorded(string gameId, MissedBlindRecordedEvent evt)
    {
        _logger.LogInformation(
            "Player {PlayerName} missed blinds recorded in game {GameId}. SB: {MissedSB}, BB: {MissedBB}",
            evt.PlayerName, gameId, evt.MissedSmallBlind, evt.MissedBigBlind);
        await Clients.Group(gameId).SendAsync("MissedBlindRecorded", evt);
    }

    /// <summary>
    /// Notifies the game group that a player has posted missed blinds.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The missed blinds posted event.</param>
    public async Task NotifyMissedBlindsPosted(string gameId, MissedBlindsPostedEvent evt)
    {
        _logger.LogInformation(
            "Player {PlayerName} posted missed blinds in game {GameId}. Total: {Total}",
            evt.PlayerName, gameId, evt.TotalAmount);
        await Clients.Group(gameId).SendAsync("MissedBlindsPosted", evt);
    }

    /// <summary>
    /// Notifies the game group that a dead button situation has occurred.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The dead button event.</param>
    public async Task NotifyDeadButton(string gameId, DeadButtonEvent evt)
    {
        _logger.LogInformation(
            "Dead button at position {Position} in game {GameId}. Reason: {Reason}",
            evt.ButtonPosition, gameId, evt.Reason);
        await Clients.Group(gameId).SendAsync("DeadButton", evt);
    }

    /// <summary>
    /// Notifies the game group of the current blind level information.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The blind level info event.</param>
    public async Task NotifyBlindLevelInfo(string gameId, BlindLevelInfoEvent evt)
    {
        _logger.LogInformation(
            "Blind level in game {GameId}: Level {Level}, SB: {SmallBlind}, BB: {BigBlind}, Ante: {Ante}",
            gameId, evt.Level, evt.SmallBlind, evt.BigBlind, evt.Ante);
        await Clients.Group(gameId).SendAsync("BlindLevelInfo", evt);
    }

    #endregion

    #region Timer Events

    /// <summary>
    /// Notifies the game group that a player's turn timer has started.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The turn timer started event.</param>
    public async Task NotifyTurnTimerStarted(string gameId, TurnTimerStartedEvent evt)
    {
        _logger.LogDebug(
            "Turn timer started for player {PlayerName} in game {GameId}. Duration: {Duration}s, TimeBank: {TimeBank}s",
            evt.PlayerName, gameId, evt.DurationSeconds, evt.TimeBankRemaining);
        await Clients.Group(gameId).SendAsync("TurnTimerStarted", evt);
    }

    /// <summary>
    /// Notifies the game group of a timer tick (seconds remaining).
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The turn timer tick event.</param>
    public async Task NotifyTurnTimerTick(string gameId, TurnTimerTickEvent evt)
    {
        // Only log every 5 seconds to reduce log volume
        if (evt.SecondsRemaining % 5 == 0)
        {
            _logger.LogTrace(
                "Timer tick for player {PlayerName} in game {GameId}: {Seconds}s remaining",
                evt.PlayerName, gameId, evt.SecondsRemaining);
        }
        await Clients.Group(gameId).SendAsync("TurnTimerTick", evt);
    }

    /// <summary>
    /// Notifies the game group that a player's timer is about to expire.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The turn timer warning event.</param>
    public async Task NotifyTurnTimerWarning(string gameId, TurnTimerWarningEvent evt)
    {
        _logger.LogInformation(
            "Turn timer warning for player {PlayerName} in game {GameId}: {Seconds}s remaining",
            evt.PlayerName, gameId, evt.SecondsRemaining);
        await Clients.Group(gameId).SendAsync("TurnTimerWarning", evt);
    }

    /// <summary>
    /// Notifies the game group that a player's timer has expired.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The turn timer expired event.</param>
    public async Task NotifyTurnTimerExpired(string gameId, TurnTimerExpiredEvent evt)
    {
        _logger.LogInformation(
            "Turn timer expired for player {PlayerName} in game {GameId}. Default action: {Action}",
            evt.PlayerName, gameId, evt.DefaultAction);
        await Clients.Group(gameId).SendAsync("TurnTimerExpired", evt);
    }

    /// <summary>
    /// Notifies the game group that a player has activated their time bank.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The time bank activated event.</param>
    public async Task NotifyTimeBankActivated(string gameId, TimeBankActivatedEvent evt)
    {
        _logger.LogInformation(
            "Time bank activated by player {PlayerName} in game {GameId}. Added: {Added}s, Remaining: {Remaining}s",
            evt.PlayerName, gameId, evt.SecondsAdded, evt.TimeBankRemaining);
        await Clients.Group(gameId).SendAsync("TimeBankActivated", evt);
    }

    /// <summary>
    /// Notifies the game group that a player's timer has been stopped.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="evt">The turn timer stopped event.</param>
    public async Task NotifyTurnTimerStopped(string gameId, TurnTimerStoppedEvent evt)
    {
        _logger.LogDebug(
            "Turn timer stopped for player {PlayerName} in game {GameId}",
            evt.PlayerName, gameId);
        await Clients.Group(gameId).SendAsync("TurnTimerStopped", evt);
    }

    /// <summary>
    /// Requests to use time bank for the calling player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    public async Task UseTimeBank(string tableId)
    {
        var connectionId = Context.ConnectionId;
        var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);

        if (playerInfo is null || playerInfo.TableId != tableId)
        {
            _logger.LogWarning(
                "Time bank request rejected: Connection {ConnectionId} is not registered at table {TableId}",
                connectionId, tableId);
            await Clients.Caller.SendAsync("TimeBankRejected", "You are not seated at this table.");
            return;
        }

        _logger.LogInformation(
            "Player {PlayerName} requested time bank at table {TableId}",
            playerInfo.PlayerName, tableId);

        // The actual time bank activation would be handled by game logic
        // This sends a request event that the server-side game logic can process
        await Clients.Caller.SendAsync("TimeBankRequested", tableId, playerInfo.PlayerName);
    }

    #endregion
}
