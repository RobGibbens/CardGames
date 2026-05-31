using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// SignalR hub for real-time lobby updates.
/// Clients join the lobby group to receive game creation/deletion notifications.
/// </summary>
[Authorize(AuthenticationSchemes = InternalApiAuthenticationHandler.SchemeName)]
public sealed class LobbyHub : Hub
{
    private readonly ILogger<LobbyHub> _logger;

    /// <summary>
    /// The name of the lobby group that all connected clients join.
    /// </summary>
    public const string LobbyGroupName = "lobby";

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyHub"/> class.
    /// </summary>
    public LobbyHub(ILogger<LobbyHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Joins the caller to the lobby group to receive game updates.
    /// </summary>
    public async Task JoinLobby()
    {
        var userId = GetUserIdentifier();
        using var scope = CreateScope(userId);

        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroupName);

        _logger.LogInformation(
            "User {UserId} (connection {ConnectionId}) joined lobby group",
            userId ?? "unknown", Context.ConnectionId);
    }

    /// <summary>
    /// Removes the caller from the lobby group.
    /// </summary>
    public async Task LeaveLobby()
    {
        var userId = GetUserIdentifier();
        using var scope = CreateScope(userId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroupName);

        _logger.LogInformation(
            "User {UserId} (connection {ConnectionId}) left lobby group",
            userId ?? "unknown", Context.ConnectionId);
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdentifier();
        using var scope = CreateScope(userId);
        _logger.LogInformation("User {UserId} connected to lobby hub with connection {ConnectionId}",
            userId ?? "unknown", Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdentifier();
        using var scope = CreateScope(userId);

        if (exception is not null)
        {
            _logger.LogWarning(exception,
                "User {UserId} disconnected from lobby hub with error (connection {ConnectionId})",
                userId ?? "unknown", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("User {UserId} disconnected from lobby hub (connection {ConnectionId})",
                userId ?? "unknown", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string? GetUserIdentifier()
    {
        return Context.UserIdentifier ?? Context.User?.Identity?.Name;
    }

    private IDisposable? CreateScope(string? userId)
    {
        return _logger.BeginScope(new Dictionary<string, object>
        {
            ["ConnectionId"] = Context.ConnectionId,
            ["UserId"] = userId ?? "anonymous"
        });
    }
}
