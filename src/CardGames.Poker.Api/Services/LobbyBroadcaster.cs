using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Broadcasts lobby updates to connected SignalR clients.
/// </summary>
public sealed class LobbyBroadcaster : ILobbyBroadcaster
{
    private readonly IHubContext<LobbyHub> _hubContext;
    private readonly ILogger<LobbyBroadcaster> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyBroadcaster"/> class.
    /// </summary>
    public LobbyBroadcaster(
        IHubContext<LobbyHub> hubContext,
        ILogger<LobbyBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task BroadcastGameCreatedAsync(GameCreatedDto gameCreated, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameCreated);

        try
        {
            await _hubContext.Clients.Group(LobbyHub.LobbyGroupName)
                .SendAsync("GameCreated", gameCreated, cancellationToken);

            _logger.LogInformation(
                "Broadcast GameCreated for game {GameId} ({GameName}) to lobby",
                gameCreated.GameId, gameCreated.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error broadcasting GameCreated for game {GameId} to lobby",
                gameCreated.GameId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task BroadcastGameDeletedAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.Group(LobbyHub.LobbyGroupName)
                .SendAsync("GameDeleted", gameId, cancellationToken);

            _logger.LogInformation(
                "Broadcast GameDeleted for game {GameId} to lobby",
                gameId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error broadcasting GameDeleted for game {GameId} to lobby",
                gameId);
            throw;
        }
    }
}
