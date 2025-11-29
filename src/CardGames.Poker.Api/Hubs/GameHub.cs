using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time poker game communication.
/// </summary>
public class GameHub : Hub
{
    private readonly ILogger<GameHub> _logger;

    public GameHub(ILogger<GameHub> logger)
    {
        _logger = logger;
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
}
