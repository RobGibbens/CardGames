# ADR-001: Use SignalR for Real-Time Communication

## Status

Accepted

## Context

CardGames Poker requires real-time communication between the server and multiple connected clients for:

- Broadcasting game state changes to all players at a table
- Delivering private information (hole cards) to specific players
- Turn-based player action notifications
- Timer synchronization across all clients
- Chat messaging
- Connection state management and reconnection handling

We need a technology that:
1. Supports bi-directional communication
2. Handles connection management automatically
3. Provides group-based messaging (tables, lobbies)
4. Works with our .NET backend stack
5. Has strong client support (JavaScript/TypeScript for Blazor)

## Decision

We will use **ASP.NET Core SignalR** for all real-time communication in the CardGames application.

SignalR provides:
- **WebSocket transport** with automatic fallback to Server-Sent Events or Long Polling
- **Hub abstraction** for organizing server-side logic
- **Groups** for managing table-based communication
- **Client proxy generation** for strongly-typed messaging
- **Built-in reconnection** with automatic re-subscription
- **Integration with ASP.NET Core** authentication and authorization
- **Scale-out support** via Redis backplane for multi-server deployments

### Implementation Details

1. **Single Hub (`GameHub`)** handles all game-related real-time communication
2. **Group-based messaging** for table isolation:
   - `tableId` group for game events
   - `lobby` group for table listings
   - `{tableId}-waitlist` group for waiting list notifications
3. **Private messaging** for hole cards using `Clients.Client(connectionId)`
4. **Connection tracking** via `IConnectionMappingService` for player-to-connection mapping

## Consequences

### Positive

- **Native .NET integration** - No external dependencies for real-time features
- **Automatic transport negotiation** - Works across different network conditions
- **Familiar programming model** - Similar to controller/action patterns
- **Strong tooling** - Good debugging support in Visual Studio
- **Scalable** - Redis backplane available for horizontal scaling
- **Reconnection handling** - Built-in support for reconnecting clients

### Negative

- **Server resources** - Each connection consumes server resources (vs serverless alternatives)
- **State management** - SignalR is stateless; we must manage connection-to-player mapping ourselves
- **Complexity at scale** - Redis backplane required for multi-server deployment
- **Blazor Server coupling** - SignalR is required for Blazor Server, creating tighter coupling

### Neutral

- **Learning curve** - Teams familiar with WebSockets will adapt easily
- **Testing** - Requires mocking hub context in unit tests

## Alternatives Considered

### 1. WebSockets Only (Raw)

**Rejected because:**
- Requires manual connection management
- No built-in group/room support
- No automatic reconnection
- More code to maintain

### 2. Socket.IO with .NET Adapter

**Rejected because:**
- Non-native to .NET ecosystem
- Additional complexity in hosting/deployment
- Less optimal for Blazor integration

### 3. gRPC Streaming

**Rejected because:**
- Not well-suited for browser clients without a proxy
- More complex setup for bi-directional streaming
- Overkill for our messaging patterns

### 4. Azure SignalR Service (Serverless)

**Considered for future:**
- Could be adopted for production scale-out
- Current development uses self-hosted SignalR
- Easy migration path exists if needed

## References

- [ASP.NET Core SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [SignalR Hub Protocol](https://docs.microsoft.com/en-us/aspnet/core/signalr/hubprotocol)
- [Scale-out with Redis](https://docs.microsoft.com/en-us/aspnet/core/signalr/redis-backplane)
