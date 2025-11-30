using CardGames.Poker.Api.Features.Showdown;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class GameHubTests
{
    private readonly GameHub _hub;
    private readonly ILogger<GameHub> _logger;
    private readonly IShowdownAuditLogger _showdownAuditLogger;
    private readonly IConnectionMappingService _connectionMapping;
    private readonly IHubCallerClients _mockClients;
    private readonly ISingleClientProxy _mockCaller;
    private readonly IClientProxy _mockAll;
    private readonly IClientProxy _mockGroup;
    private readonly HubCallerContext _mockContext;
    private readonly IGroupManager _mockGroups;

    public GameHubTests()
    {
        _logger = Substitute.For<ILogger<GameHub>>();
        _showdownAuditLogger = Substitute.For<IShowdownAuditLogger>();
        _connectionMapping = Substitute.For<IConnectionMappingService>();
        
        // Create mock proxies first
        _mockCaller = Substitute.For<ISingleClientProxy>();
        _mockAll = Substitute.For<IClientProxy>();
        _mockGroup = Substitute.For<IClientProxy>();
        
        // Create clients mock and set up returns
        _mockClients = Substitute.For<IHubCallerClients>();
        _mockClients.Caller.Returns(_mockCaller);
        _mockClients.All.Returns(_mockAll);
        _mockClients.Group(Arg.Any<string>()).Returns(_mockGroup);
        
        // Create context and groups mocks
        _mockContext = Substitute.For<HubCallerContext>();
        _mockContext.ConnectionId.Returns(TestConnectionId);
        
        _mockGroups = Substitute.For<IGroupManager>();

        _hub = new GameHub(_logger, _showdownAuditLogger, _connectionMapping);

        // Use reflection to set the Clients, Context and Groups properties
        var clientsProperty = typeof(Hub).GetProperty("Clients");
        clientsProperty!.SetValue(_hub, _mockClients);
        
        var contextProperty = typeof(Hub).GetProperty("Context");
        contextProperty!.SetValue(_hub, _mockContext);
        
        var groupsProperty = typeof(Hub).GetProperty("Groups");
        groupsProperty!.SetValue(_hub, _mockGroups);
    }

    private static readonly string TestTableId = Guid.NewGuid().ToString();
    private static readonly string TestConnectionId = "test-connection-id";

    [Fact]
    public async Task JoinTable_NewConnection_AddsToMappingAndGroup()
    {
        // Arrange
        var tableId = TestTableId;
        var playerName = "TestPlayer";
        _connectionMapping.TryReconnect(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((PlayerConnectionInfo?)null);

        // Act
        await _hub.JoinTable(tableId, playerName);

        // Assert
        _connectionMapping.Received(1).AddConnection(TestConnectionId, playerName, tableId);
        await _mockGroups.Received(1).AddToGroupAsync(TestConnectionId, tableId, default);
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerConnected",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task JoinTable_Reconnection_DoesNotAddNewMapping()
    {
        // Arrange
        var tableId = TestTableId;
        var playerName = "TestPlayer";
        var oldInfo = new PlayerConnectionInfo(
            "old-conn-id",
            playerName,
            tableId,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddMinutes(-1),
            true,
            DateTime.UtcNow.AddSeconds(-30));

        _connectionMapping.TryReconnect(TestConnectionId, playerName, tableId).Returns(oldInfo);

        // Act
        await _hub.JoinTable(tableId, playerName);

        // Assert
        _connectionMapping.DidNotReceive().AddConnection(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerReconnected",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task LeaveTable_RemovesConnectionAndNotifiesGroup()
    {
        // Arrange
        var tableId = TestTableId;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.LeaveTable(tableId);

        // Assert
        _connectionMapping.Received(1).RemoveConnection(TestConnectionId);
        await _mockGroups.Received(1).RemoveFromGroupAsync(TestConnectionId, tableId, default);
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerDisconnected",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task Fold_WithValidConnection_BroadcastsActionToGroup()
    {
        // Arrange
        var tableId = TestTableId;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.Fold(tableId);

        // Assert
        _connectionMapping.Received(1).UpdateLastActivity(TestConnectionId);
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerAction",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task Check_WithValidConnection_BroadcastsActionToGroup()
    {
        // Arrange
        var tableId = TestTableId;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.Check(tableId);

        // Assert
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerAction",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task Call_WithValidConnection_BroadcastsActionWithAmount()
    {
        // Arrange
        var tableId = TestTableId;
        var amount = 50;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.Call(tableId, amount);

        // Assert
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerAction",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task Bet_WithValidConnection_BroadcastsActionWithAmount()
    {
        // Arrange
        var tableId = TestTableId;
        var amount = 100;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.Bet(tableId, amount);

        // Assert
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerAction",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task Raise_WithValidConnection_BroadcastsActionWithAmount()
    {
        // Arrange
        var tableId = TestTableId;
        var amount = 200;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.Raise(tableId, amount);

        // Assert
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerAction",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task AllIn_WithValidConnection_BroadcastsActionWithAmount()
    {
        // Arrange
        var tableId = TestTableId;
        var amount = 500;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            tableId,
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.AllIn(tableId, amount);

        // Assert
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerAction",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task PlayerAction_WithInvalidConnection_RejectsAction()
    {
        // Arrange
        var tableId = TestTableId;
        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns((PlayerConnectionInfo?)null);

        // Act
        await _hub.Fold(tableId);

        // Assert
        await _mockCaller.Received(1).SendCoreAsync(
            "ActionRejected",
            Arg.Any<object[]>(),
            default);
        await _mockGroup.DidNotReceive().SendCoreAsync("PlayerAction", Arg.Any<object[]>(), default);
    }

    [Fact]
    public async Task PlayerAction_WithWrongTable_RejectsAction()
    {
        // Arrange
        var tableId = TestTableId;
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            "different-table",
            DateTime.UtcNow,
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.Fold(tableId);

        // Assert
        await _mockCaller.Received(1).SendCoreAsync(
            "ActionRejected",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task Heartbeat_UpdatesLastActivity()
    {
        // Act
        await _hub.Heartbeat();

        // Assert
        _connectionMapping.Received(1).UpdateLastActivity(TestConnectionId);
        await _mockCaller.Received(1).SendCoreAsync(
            "HeartbeatAck",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task SendPrivateData_SendsToCorrectPlayer()
    {
        // Arrange
        var tableId = TestTableId;
        var playerName = "TargetPlayer";
        var connectionId = "target-connection-id";
        var holeCards = new List<string> { "Ah", "Kd" };

        _connectionMapping.GetConnectionId(playerName, tableId).Returns(connectionId);

        var mockClient = Substitute.For<ISingleClientProxy>();
        _mockClients.Client(connectionId).Returns(mockClient);

        // Act
        await _hub.SendPrivateData(tableId, playerName, holeCards, "Pair of Aces");

        // Assert
        await mockClient.Received(1).SendCoreAsync(
            "PrivateData",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task RequestTableState_SendsRequestToCaller()
    {
        // Arrange
        var tableId = TestTableId;

        // Act
        await _hub.RequestTableState(tableId);

        // Assert
        await _mockCaller.Received(1).SendCoreAsync(
            "TableStateRequested",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task GetConnectionInfo_ReturnsPlayerInfo()
    {
        // Arrange
        var playerInfo = new PlayerConnectionInfo(
            TestConnectionId,
            "TestPlayer",
            TestTableId,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow);

        _connectionMapping.GetPlayerInfo(TestConnectionId).Returns(playerInfo);

        // Act
        await _hub.GetConnectionInfo();

        // Assert
        await _mockCaller.Received(1).SendCoreAsync(
            "ConnectionInfo",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task SendMessage_BroadcastsToAll()
    {
        // Arrange
        var message = "Test message";

        // Act
        await _hub.SendMessage(message);

        // Assert
        await _mockAll.Received(1).SendCoreAsync(
            "ReceiveMessage",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task JoinGame_AddsToGroupAndNotifies()
    {
        // Arrange
        var gameId = "game-123";

        // Act
        await _hub.JoinGame(gameId);

        // Assert
        await _mockGroups.Received(1).AddToGroupAsync(TestConnectionId, gameId, default);
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerJoined",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task LeaveGame_RemovesFromGroupAndNotifies()
    {
        // Arrange
        var gameId = "game-123";

        // Act
        await _hub.LeaveGame(gameId);

        // Assert
        await _mockGroups.Received(1).RemoveFromGroupAsync(TestConnectionId, gameId, default);
        await _mockGroup.Received(1).SendCoreAsync(
            "PlayerLeft",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task JoinLobby_AddsToLobbyGroupAndNotifiesCaller()
    {
        // Act
        await _hub.JoinLobby();

        // Assert
        await _mockGroups.Received(1).AddToGroupAsync(TestConnectionId, "lobby", default);
        await _mockCaller.Received(1).SendCoreAsync(
            "JoinedLobby",
            Arg.Any<object[]>(),
            default);
    }

    [Fact]
    public async Task LeaveLobby_RemovesFromLobbyGroup()
    {
        // Act
        await _hub.LeaveLobby();

        // Assert
        await _mockGroups.Received(1).RemoveFromGroupAsync(TestConnectionId, "lobby", default);
    }
}
