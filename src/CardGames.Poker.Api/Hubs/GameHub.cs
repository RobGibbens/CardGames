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
}
