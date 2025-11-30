using CardGames.Poker.Api.Hubs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class ConnectionMappingServiceTests
{
    private readonly ConnectionMappingService _service;
    private readonly ILogger<ConnectionMappingService> _logger;

    public ConnectionMappingServiceTests()
    {
        _logger = Substitute.For<ILogger<ConnectionMappingService>>();
        _service = new ConnectionMappingService(_logger);
    }

    [Fact]
    public void AddConnection_AddsPlayerToMapping()
    {
        // Arrange
        var connectionId = "conn-123";
        var playerName = "TestPlayer";
        var tableId = "table-456";

        // Act
        _service.AddConnection(connectionId, playerName, tableId);

        // Assert
        var info = _service.GetPlayerInfo(connectionId);
        info.Should().NotBeNull();
        info!.ConnectionId.Should().Be(connectionId);
        info.PlayerName.Should().Be(playerName);
        info.TableId.Should().Be(tableId);
        info.IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public void GetConnectionId_ReturnsCorrectConnectionId()
    {
        // Arrange
        var connectionId = "conn-123";
        var playerName = "TestPlayer";
        var tableId = "table-456";
        _service.AddConnection(connectionId, playerName, tableId);

        // Act
        var result = _service.GetConnectionId(playerName, tableId);

        // Assert
        result.Should().Be(connectionId);
    }

    [Fact]
    public void GetConnectionId_ReturnsNullForUnknownPlayer()
    {
        // Act
        var result = _service.GetConnectionId("UnknownPlayer", "unknown-table");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RemoveConnection_RemovesPlayerFromMapping()
    {
        // Arrange
        var connectionId = "conn-123";
        var playerName = "TestPlayer";
        var tableId = "table-456";
        _service.AddConnection(connectionId, playerName, tableId);

        // Act
        _service.RemoveConnection(connectionId);

        // Assert
        var info = _service.GetPlayerInfo(connectionId);
        info.Should().BeNull();
        _service.GetConnectionId(playerName, tableId).Should().BeNull();
    }

    [Fact]
    public void GetTableConnections_ReturnsAllConnectionsForTable()
    {
        // Arrange
        var tableId = "table-456";
        _service.AddConnection("conn-1", "Player1", tableId);
        _service.AddConnection("conn-2", "Player2", tableId);
        _service.AddConnection("conn-3", "Player3", "other-table");

        // Act
        var connections = _service.GetTableConnections(tableId);

        // Assert
        connections.Should().HaveCount(2);
        connections.Should().Contain("conn-1");
        connections.Should().Contain("conn-2");
    }

    [Fact]
    public void GetTablePlayers_ReturnsAllPlayersForTable()
    {
        // Arrange
        var tableId = "table-456";
        _service.AddConnection("conn-1", "Player1", tableId);
        _service.AddConnection("conn-2", "Player2", tableId);
        _service.AddConnection("conn-3", "Player3", "other-table");

        // Act
        var players = _service.GetTablePlayers(tableId);

        // Assert
        players.Should().HaveCount(2);
        players.Select(p => p.PlayerName).Should().Contain(["Player1", "Player2"]);
    }

    [Fact]
    public void UpdateLastActivity_UpdatesTimestamp()
    {
        // Arrange
        var connectionId = "conn-123";
        _service.AddConnection(connectionId, "TestPlayer", "table-456");
        var originalInfo = _service.GetPlayerInfo(connectionId);
        var originalLastActivity = originalInfo!.LastActivity;

        // Wait a tiny bit to ensure timestamp differs
        Thread.Sleep(10);

        // Act
        _service.UpdateLastActivity(connectionId);

        // Assert
        var updatedInfo = _service.GetPlayerInfo(connectionId);
        updatedInfo!.LastActivity.Should().BeAfter(originalLastActivity);
    }

    [Fact]
    public void GetStaleConnections_ReturnsConnectionsInactiveForLongerThanTimeout()
    {
        // Arrange
        _service.AddConnection("conn-old", "OldPlayer", "table-1");
        Thread.Sleep(50);
        _service.AddConnection("conn-new", "NewPlayer", "table-1");

        // Act
        var staleConnections = _service.GetStaleConnections(TimeSpan.FromMilliseconds(40));

        // Assert
        staleConnections.Should().Contain("conn-old");
        staleConnections.Should().NotContain("conn-new");
    }

    [Fact]
    public void MarkDisconnected_MovesPlayerToDisconnectedList()
    {
        // Arrange
        var connectionId = "conn-123";
        var playerName = "TestPlayer";
        var tableId = "table-456";
        _service.AddConnection(connectionId, playerName, tableId);

        // Act
        _service.MarkDisconnected(connectionId);

        // Assert
        _service.GetPlayerInfo(connectionId).Should().BeNull();
        var disconnected = _service.GetDisconnectedPlayers(tableId);
        disconnected.Should().HaveCount(1);
        disconnected.First().PlayerName.Should().Be(playerName);
        disconnected.First().IsDisconnected.Should().BeTrue();
    }

    [Fact]
    public void TryReconnect_ReconnectsDisconnectedPlayer()
    {
        // Arrange
        var oldConnectionId = "conn-old";
        var playerName = "TestPlayer";
        var tableId = "table-456";
        _service.AddConnection(oldConnectionId, playerName, tableId);
        _service.MarkDisconnected(oldConnectionId);

        var newConnectionId = "conn-new";

        // Act
        var oldInfo = _service.TryReconnect(newConnectionId, playerName, tableId);

        // Assert
        oldInfo.Should().NotBeNull();
        oldInfo!.PlayerName.Should().Be(playerName);
        oldInfo.IsDisconnected.Should().BeTrue();

        var newInfo = _service.GetPlayerInfo(newConnectionId);
        newInfo.Should().NotBeNull();
        newInfo!.PlayerName.Should().Be(playerName);
        newInfo.IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public void TryReconnect_ReturnsNullForUnknownPlayer()
    {
        // Act
        var result = _service.TryReconnect("new-conn", "UnknownPlayer", "unknown-table");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RemoveDisconnectedPlayer_RemovesPlayerFromDisconnectedList()
    {
        // Arrange
        var connectionId = "conn-123";
        var playerName = "TestPlayer";
        var tableId = "table-456";
        _service.AddConnection(connectionId, playerName, tableId);
        _service.MarkDisconnected(connectionId);

        // Act
        _service.RemoveDisconnectedPlayer(playerName, tableId);

        // Assert
        var disconnected = _service.GetDisconnectedPlayers(tableId);
        disconnected.Should().BeEmpty();
    }

    [Fact]
    public void GetTableConnections_ReturnsEmptyListForUnknownTable()
    {
        // Act
        var connections = _service.GetTableConnections("unknown-table");

        // Assert
        connections.Should().BeEmpty();
    }

    [Fact]
    public void GetDisconnectedPlayers_ReturnsOnlyPlayersFromSpecificTable()
    {
        // Arrange
        var tableId1 = "table-1";
        var tableId2 = "table-2";
        
        _service.AddConnection("conn-1", "Player1", tableId1);
        _service.AddConnection("conn-2", "Player2", tableId2);
        
        _service.MarkDisconnected("conn-1");
        _service.MarkDisconnected("conn-2");

        // Act
        var disconnectedTable1 = _service.GetDisconnectedPlayers(tableId1);
        var disconnectedTable2 = _service.GetDisconnectedPlayers(tableId2);

        // Assert
        disconnectedTable1.Should().HaveCount(1);
        disconnectedTable1.First().PlayerName.Should().Be("Player1");
        
        disconnectedTable2.Should().HaveCount(1);
        disconnectedTable2.First().PlayerName.Should().Be("Player2");
    }

    [Fact]
    public void MultipleConnectionsFromSamePlayer_LastOneWins()
    {
        // Arrange
        var playerName = "TestPlayer";
        var tableId = "table-456";
        var firstConnectionId = "conn-1";
        var secondConnectionId = "conn-2";

        // Act
        _service.AddConnection(firstConnectionId, playerName, tableId);
        _service.AddConnection(secondConnectionId, playerName, tableId);

        // Assert - second connection should replace the first in table mapping
        var connectionId = _service.GetConnectionId(playerName, tableId);
        connectionId.Should().Be(secondConnectionId);
    }
}
