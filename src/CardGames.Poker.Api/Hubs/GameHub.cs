using CardGames.Poker.Api.Features.Showdown;
using CardGames.Poker.Shared.DTOs;
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

    public GameHub(ILogger<GameHub> logger, IShowdownAuditLogger showdownAuditLogger)
    {
        _logger = logger;
        _showdownAuditLogger = showdownAuditLogger;
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
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
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
}
