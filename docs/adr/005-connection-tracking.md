# ADR-005: Connection Tracking for Player Sessions

## Status

Accepted

## Context

In a real-time poker game, we need to:

1. **Map SignalR connections to players** - Know which connection belongs to which player
2. **Track players at tables** - Know which players are seated at which tables
3. **Handle disconnections gracefully** - Allow players to reconnect without losing their seat
4. **Support reconnection** - Resume player sessions after brief disconnections
5. **Detect inactive players** - Handle players who disconnect without explicitly leaving
6. **Route private messages** - Send hole cards only to the correct player

SignalR's built-in groups are insufficient because:
- Groups don't track individual connection metadata
- No built-in reconnection matching
- Can't query "which connection ID is player X?"

## Decision

We will implement a **dedicated connection mapping service** (`IConnectionMappingService`) that:

1. **Tracks connection ↔ player ↔ table relationships**
2. **Supports reconnection detection and matching**
3. **Maintains activity timestamps**
4. **Provides reverse lookups** (player → connection, connection → player)

### Interface Design

```csharp
public interface IConnectionMappingService
{
    // Connection management
    void AddConnection(string connectionId, string playerName, string tableId);
    void RemoveConnection(string connectionId);
    void MarkDisconnected(string connectionId);
    
    // Reconnection support
    PlayerConnectionInfo? TryReconnect(string newConnectionId, string playerName, string tableId);
    
    // Lookups
    PlayerConnectionInfo? GetPlayerInfo(string connectionId);
    string? GetConnectionId(string playerName, string tableId);
    
    // Activity tracking
    void UpdateLastActivity(string connectionId);
    IEnumerable<PlayerConnectionInfo> GetStaleConnections(TimeSpan threshold);
}
```

### Data Structure

```csharp
public record PlayerConnectionInfo(
    string ConnectionId,
    string PlayerName,
    string TableId,
    DateTime ConnectedAt,
    DateTime LastActivity,
    bool IsDisconnected = false,
    DateTime? DisconnectedAt = null);
```

### Storage Strategy

In-memory dictionary with composite key:

```csharp
// By connection ID (primary lookup)
private readonly ConcurrentDictionary<string, PlayerConnectionInfo> _connections = new();

// By player+table (for reconnection matching)
private readonly ConcurrentDictionary<(string PlayerName, string TableId), string> _playerToConnection = new();
```

## Consequences

### Positive

- **Fast lookups** - O(1) for both directions
- **Reconnection support** - Match returning players to their session
- **Activity tracking** - Detect stale connections
- **Isolation** - Each table's connections are independent
- **Testable** - Interface allows mocking in unit tests
- **Private messaging** - Route messages to correct connection

### Negative

- **Memory usage** - Must store connection info in memory
- **Synchronization** - Concurrent dictionary needed for thread safety
- **Cleanup required** - Must remove stale connections periodically
- **Single-server limitation** - Requires distributed cache for multi-server

### Neutral

- **Startup cost** - Connections must be re-established after server restart
- **Complexity** - Additional component to maintain

## Usage Patterns

### Player Joins Table

```csharp
public async Task JoinTable(string tableId, string playerName)
{
    var connectionId = Context.ConnectionId;
    
    // Check for reconnection first
    var oldInfo = _connectionMapping.TryReconnect(connectionId, playerName, tableId);
    if (oldInfo is not null)
    {
        // Reconnection - notify group
        await Clients.Group(tableId).SendAsync("PlayerReconnected", ...);
        return;
    }
    
    // New connection
    _connectionMapping.AddConnection(connectionId, playerName, tableId);
    await Groups.AddToGroupAsync(connectionId, tableId);
    await Clients.Group(tableId).SendAsync("PlayerConnected", ...);
}
```

### Player Disconnects

```csharp
public override async Task OnDisconnectedAsync(Exception? exception)
{
    var connectionId = Context.ConnectionId;
    var playerInfo = _connectionMapping.GetPlayerInfo(connectionId);
    
    if (playerInfo is not null)
    {
        // Mark as disconnected, don't remove immediately
        // Allows reconnection window
        _connectionMapping.MarkDisconnected(connectionId);
        
        await Clients.Group(playerInfo.TableId).SendAsync("PlayerDisconnected", ...);
    }
}
```

### Private Messaging (Hole Cards)

```csharp
public async Task SendPrivateData(string tableId, string playerName, IReadOnlyList<string> holeCards)
{
    var connectionId = _connectionMapping.GetConnectionId(playerName, tableId);
    if (connectionId is not null)
    {
        await Clients.Client(connectionId).SendAsync("PrivateData", ...);
    }
}
```

### Stale Connection Cleanup

```csharp
// Background service
public class ConnectionHealthMonitorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var staleConnections = _connectionMapping.GetStaleConnections(TimeSpan.FromMinutes(5));
            
            foreach (var connection in staleConnections)
            {
                _connectionMapping.RemoveConnection(connection.ConnectionId);
                // Notify table of player timeout
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### Heartbeat for Activity

```csharp
public async Task Heartbeat()
{
    var connectionId = Context.ConnectionId;
    _connectionMapping.UpdateLastActivity(connectionId);
    await Clients.Caller.SendAsync("HeartbeatAck", DateTime.UtcNow);
}
```

## Reconnection Flow

```
1. Player disconnects unexpectedly
   └─> Mark connection as disconnected (don't remove)
   └─> Notify table of disconnection

2. Player reconnects within window (e.g., 2 minutes)
   └─> TryReconnect() finds matching player+table
   └─> Update connection ID to new connection
   └─> Notify table of reconnection
   └─> Send table state sync to player

3. Reconnection window expires
   └─> Background service removes stale connection
   └─> Player's seat may be released
```

## Scaling Considerations

For multi-server deployment:

1. **Distributed cache** (Redis) for connection mapping
2. **Sticky sessions** to route player to same server
3. **SignalR backplane** (Redis) for cross-server messaging

Current implementation is single-server only but designed for future migration.

## Alternatives Considered

### 1. Store in SignalR Context.Items

**Rejected because:**
- Not queryable across connections
- Lost on disconnect
- No persistence for reconnection

### 2. Database Storage

**Rejected because:**
- Too slow for real-time lookups
- Unnecessary persistence for transient data
- Overkill for connection tracking

### 3. ASP.NET Core Session

**Rejected because:**
- Per-request, not per-connection
- Not designed for WebSocket connections
- Would require adapter pattern

### 4. External Service (Redis)

**Deferred:**
- Will adopt for production scale-out
- Current in-memory approach sufficient for development
- Easy migration path available

## References

- [Mapping SignalR Connections to Users](https://docs.microsoft.com/en-us/aspnet/core/signalr/groups)
- [Background Services in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [ConcurrentDictionary Best Practices](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)
