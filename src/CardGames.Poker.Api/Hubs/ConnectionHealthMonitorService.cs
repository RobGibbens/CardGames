using CardGames.Poker.Shared.Events;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// Background service that monitors connection health and detects stale connections.
/// </summary>
public class ConnectionHealthMonitorService : BackgroundService
{
    private readonly IConnectionMappingService _connectionMapping;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<ConnectionHealthMonitorService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _staleTimeout;
    private readonly TimeSpan _disconnectedPlayerTimeout;

    public ConnectionHealthMonitorService(
        IConnectionMappingService connectionMapping,
        IHubContext<GameHub> hubContext,
        ILogger<ConnectionHealthMonitorService> logger,
        IConfiguration configuration)
    {
        _connectionMapping = connectionMapping;
        _hubContext = hubContext;
        _logger = logger;

        // Configure timeouts from configuration or use defaults
        _checkInterval = TimeSpan.FromSeconds(
            configuration.GetValue("SignalR:HealthCheck:IntervalSeconds", 30));
        _staleTimeout = TimeSpan.FromSeconds(
            configuration.GetValue("SignalR:HealthCheck:StaleTimeoutSeconds", 60));
        _disconnectedPlayerTimeout = TimeSpan.FromMinutes(
            configuration.GetValue("SignalR:HealthCheck:DisconnectedPlayerTimeoutMinutes", 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Connection health monitor started. Check interval: {CheckInterval}, Stale timeout: {StaleTimeout}",
            _checkInterval, _staleTimeout);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckConnectionHealthAsync(stoppingToken);
                await CleanupDisconnectedPlayersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in connection health check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckConnectionHealthAsync(CancellationToken cancellationToken)
    {
        var staleConnections = _connectionMapping.GetStaleConnections(_staleTimeout);

        foreach (var connectionId in staleConnections)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);
            if (playerInfo is null)
                continue;

            var inactiveDuration = DateTime.UtcNow - playerInfo.LastActivity;

            _logger.LogWarning(
                "Stale connection detected: {ConnectionId} for player {PlayerName} at table {TableId}. Inactive for {Duration}",
                connectionId, playerInfo.PlayerName, playerInfo.TableId, inactiveDuration);

            // Notify the table about the stale connection
            var evt = new ConnectionStaleEvent(
                Guid.Parse(playerInfo.TableId),
                DateTime.UtcNow,
                playerInfo.PlayerName,
                inactiveDuration);

            await _hubContext.Clients.Group(playerInfo.TableId)
                .SendAsync("ConnectionStale", evt, cancellationToken);

            // Send a ping request to the client
            await _hubContext.Clients.Client(connectionId)
                .SendAsync("PingRequest", DateTime.UtcNow, cancellationToken);
        }
    }

    private async Task CleanupDisconnectedPlayersAsync(CancellationToken cancellationToken)
    {
        // This would iterate over all tables and clean up players who have been
        // disconnected for too long. In a real implementation, we'd need to track
        // all active table IDs.

        // For now, we'll log that cleanup would happen
        _logger.LogDebug("Checking for disconnected players to clean up");
    }
}
