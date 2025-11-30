using System.Collections.Concurrent;

namespace CardGames.Poker.Api.Hubs;

/// <summary>
/// Service for tracking SignalR connection mappings to players and tables.
/// </summary>
public interface IConnectionMappingService
{
    /// <summary>
    /// Adds a connection mapping for a player at a table.
    /// </summary>
    void AddConnection(string connectionId, string playerName, string tableId);

    /// <summary>
    /// Removes a connection mapping.
    /// </summary>
    void RemoveConnection(string connectionId);

    /// <summary>
    /// Gets the player information for a connection.
    /// </summary>
    PlayerConnectionInfo? GetPlayerInfo(string connectionId);

    /// <summary>
    /// Gets the connection ID for a player at a specific table.
    /// </summary>
    string? GetConnectionId(string playerName, string tableId);

    /// <summary>
    /// Gets all connection IDs for players at a specific table.
    /// </summary>
    IReadOnlyList<string> GetTableConnections(string tableId);

    /// <summary>
    /// Gets all players currently at a specific table.
    /// </summary>
    IReadOnlyList<PlayerConnectionInfo> GetTablePlayers(string tableId);

    /// <summary>
    /// Updates the last activity time for a connection.
    /// </summary>
    void UpdateLastActivity(string connectionId);

    /// <summary>
    /// Gets connections that have been inactive for longer than the specified timeout.
    /// </summary>
    IReadOnlyList<string> GetStaleConnections(TimeSpan timeout);

    /// <summary>
    /// Marks a player as disconnected but preserves their state for potential reconnection.
    /// </summary>
    void MarkDisconnected(string connectionId);

    /// <summary>
    /// Attempts to reconnect a player, returning the old connection info if found.
    /// </summary>
    PlayerConnectionInfo? TryReconnect(string newConnectionId, string playerName, string tableId);

    /// <summary>
    /// Gets all disconnected players at a specific table.
    /// </summary>
    IReadOnlyList<PlayerConnectionInfo> GetDisconnectedPlayers(string tableId);

    /// <summary>
    /// Removes a disconnected player's preserved state.
    /// </summary>
    void RemoveDisconnectedPlayer(string playerName, string tableId);
}

/// <summary>
/// Represents connection information for a player.
/// </summary>
public record PlayerConnectionInfo(
    string ConnectionId,
    string PlayerName,
    string TableId,
    DateTime ConnectedAt,
    DateTime LastActivity,
    bool IsDisconnected = false,
    DateTime? DisconnectedAt = null);

/// <summary>
/// Thread-safe in-memory implementation of the connection mapping service.
/// </summary>
public class ConnectionMappingService : IConnectionMappingService
{
    private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _connectionMap = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _tableConnections = new();
    private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _disconnectedPlayers = new();
    private readonly ILogger<ConnectionMappingService> _logger;

    public ConnectionMappingService(ILogger<ConnectionMappingService> logger)
    {
        _logger = logger;
    }

    public void AddConnection(string connectionId, string playerName, string tableId)
    {
        var now = DateTime.UtcNow;
        var info = new PlayerConnectionInfo(connectionId, playerName, tableId, now, now);

        _connectionMap[connectionId] = info;

        var tableDict = _tableConnections.GetOrAdd(tableId, _ => new ConcurrentDictionary<string, string>());
        tableDict[playerName] = connectionId;

        _logger.LogInformation("Added connection {ConnectionId} for player {PlayerName} at table {TableId}",
            connectionId, playerName, tableId);
    }

    public void RemoveConnection(string connectionId)
    {
        if (_connectionMap.TryRemove(connectionId, out var info))
        {
            if (_tableConnections.TryGetValue(info.TableId, out var tableDict))
            {
                tableDict.TryRemove(info.PlayerName, out _);

                if (tableDict.IsEmpty)
                {
                    _tableConnections.TryRemove(info.TableId, out _);
                }
            }

            _logger.LogInformation("Removed connection {ConnectionId} for player {PlayerName} from table {TableId}",
                connectionId, info.PlayerName, info.TableId);
        }
    }

    public PlayerConnectionInfo? GetPlayerInfo(string connectionId)
    {
        return _connectionMap.TryGetValue(connectionId, out var info) ? info : null;
    }

    public string? GetConnectionId(string playerName, string tableId)
    {
        if (_tableConnections.TryGetValue(tableId, out var tableDict))
        {
            return tableDict.TryGetValue(playerName, out var connectionId) ? connectionId : null;
        }
        return null;
    }

    public IReadOnlyList<string> GetTableConnections(string tableId)
    {
        if (_tableConnections.TryGetValue(tableId, out var tableDict))
        {
            return tableDict.Values.ToList();
        }
        return [];
    }

    public IReadOnlyList<PlayerConnectionInfo> GetTablePlayers(string tableId)
    {
        if (_tableConnections.TryGetValue(tableId, out var tableDict))
        {
            return tableDict.Values
                .Select(connId => _connectionMap.TryGetValue(connId, out var info) ? info : null)
                .Where(info => info is not null)
                .ToList()!;
        }
        return [];
    }

    public void UpdateLastActivity(string connectionId)
    {
        if (_connectionMap.TryGetValue(connectionId, out var info))
        {
            _connectionMap[connectionId] = info with { LastActivity = DateTime.UtcNow };
        }
    }

    public IReadOnlyList<string> GetStaleConnections(TimeSpan timeout)
    {
        var cutoff = DateTime.UtcNow - timeout;
        return _connectionMap
            .Where(kvp => kvp.Value.LastActivity < cutoff && !kvp.Value.IsDisconnected)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public void MarkDisconnected(string connectionId)
    {
        if (_connectionMap.TryRemove(connectionId, out var info))
        {
            var disconnectedInfo = info with
            {
                IsDisconnected = true,
                DisconnectedAt = DateTime.UtcNow
            };

            var key = $"{info.PlayerName}:{info.TableId}";
            _disconnectedPlayers[key] = disconnectedInfo;

            if (_tableConnections.TryGetValue(info.TableId, out var tableDict))
            {
                tableDict.TryRemove(info.PlayerName, out _);
            }

            _logger.LogInformation("Marked player {PlayerName} as disconnected from table {TableId}",
                info.PlayerName, info.TableId);
        }
    }

    public PlayerConnectionInfo? TryReconnect(string newConnectionId, string playerName, string tableId)
    {
        var key = $"{playerName}:{tableId}";
        if (_disconnectedPlayers.TryRemove(key, out var oldInfo))
        {
            var now = DateTime.UtcNow;
            var newInfo = new PlayerConnectionInfo(
                newConnectionId,
                playerName,
                tableId,
                oldInfo.ConnectedAt,
                now);

            _connectionMap[newConnectionId] = newInfo;

            var tableDict = _tableConnections.GetOrAdd(tableId, _ => new ConcurrentDictionary<string, string>());
            tableDict[playerName] = newConnectionId;

            _logger.LogInformation("Player {PlayerName} reconnected to table {TableId} with new connection {ConnectionId}",
                playerName, tableId, newConnectionId);

            return oldInfo;
        }
        return null;
    }

    public IReadOnlyList<PlayerConnectionInfo> GetDisconnectedPlayers(string tableId)
    {
        return _disconnectedPlayers.Values
            .Where(info => info.TableId == tableId)
            .ToList();
    }

    public void RemoveDisconnectedPlayer(string playerName, string tableId)
    {
        var key = $"{playerName}:{tableId}";
        if (_disconnectedPlayers.TryRemove(key, out var info))
        {
            _logger.LogInformation("Removed disconnected player {PlayerName} from table {TableId}",
                playerName, tableId);
        }
    }
}
