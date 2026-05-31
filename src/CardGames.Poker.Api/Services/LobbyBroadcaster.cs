using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Broadcasts lobby updates to connected SignalR clients.
/// </summary>
public sealed class LobbyBroadcaster : ILobbyBroadcaster
{
    private readonly IHubContext<LobbyHub> _hubContext;
    private readonly ILogger<LobbyBroadcaster> _logger;
    private readonly BroadcastTelemetry _telemetry;

    /// <summary>
    /// Initializes a new instance of the <see cref="LobbyBroadcaster"/> class.
    /// </summary>
    public LobbyBroadcaster(
        IHubContext<LobbyHub> hubContext,
        ILogger<LobbyBroadcaster> logger,
        BroadcastTelemetry telemetry)
    {
        _hubContext = hubContext;
        _logger = logger;
        _telemetry = telemetry;
    }

    /// <inheritdoc />
    public async Task BroadcastGameCreatedAsync(GameCreatedDto gameCreated, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameCreated);

        await SendLobbyBroadcastAsync("GameCreated", gameCreated, gameCreated.GameId, cancellationToken);

        _logger.LogInformation(
            "Broadcast GameCreated for game {GameId} ({GameName}) to lobby",
            gameCreated.GameId, gameCreated.Name);
    }

    /// <inheritdoc />
    public async Task BroadcastGameDeletedAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await SendLobbyBroadcastAsync("GameDeleted", gameId, gameId, cancellationToken);

        _logger.LogInformation(
            "Broadcast GameDeleted for game {GameId} to lobby",
            gameId);
    }

    private async Task SendLobbyBroadcastAsync(string eventName, object payload, Guid gameId, CancellationToken cancellationToken)
    {
        using var activity = PokerActivitySource.Source.StartActivity("realtime.broadcast");
        activity?.SetTag("hub", "lobby");
        activity?.SetTag("event", eventName);
        activity?.SetTag("game.id", gameId);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _hubContext.Clients.Group(LobbyHub.LobbyGroupName)
                .SendAsync(eventName, payload, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _telemetry.RecordBroadcast("lobby", eventName, "ok", stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _telemetry.RecordBroadcast("lobby", eventName, "failed", stopwatch.Elapsed.TotalMilliseconds);
            _logger.LogError(ex, "Error broadcasting {EventName} for game {GameId} to lobby", eventName, gameId);
            throw;
        }
    }
}
