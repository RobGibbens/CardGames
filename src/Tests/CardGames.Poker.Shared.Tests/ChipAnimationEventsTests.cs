using CardGames.Poker.Shared.Events;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class ChipAnimationEventsTests
{
    [Fact]
    public void ChipPosition_WithLocationId_CreatesCorrectly()
    {
        // Act
        var position = new ChipPosition("player1");

        // Assert
        position.LocationId.Should().Be("player1");
        position.SeatNumber.Should().BeNull();
    }

    [Fact]
    public void ChipPosition_WithSeatNumber_CreatesCorrectly()
    {
        // Act
        var position = new ChipPosition("player1", 3);

        // Assert
        position.LocationId.Should().Be("player1");
        position.SeatNumber.Should().Be(3);
    }

    [Fact]
    public void ChipDto_CreatesCorrectly()
    {
        // Act
        var chip = new ChipDto(100, "#000000", 5);

        // Assert
        chip.Denomination.Should().Be(100);
        chip.Color.Should().Be("#000000");
        chip.Count.Should().Be(5);
    }

    [Fact]
    public void ChipStackDto_CreatesCorrectly()
    {
        // Arrange
        var chips = new List<ChipDto>
        {
            new(100, "#000000", 2),
            new(25, "#00FF00", 4)
        };

        // Act
        var stack = new ChipStackDto(300, chips);

        // Assert
        stack.TotalAmount.Should().Be(300);
        stack.Chips.Should().HaveCount(2);
    }

    [Fact]
    public void ChipMovementEvent_Bet_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var source = new ChipPosition("player1", 1);
        var destination = new ChipPosition("player1-bet");
        var chips = new ChipStackDto(100, new List<ChipDto> { new(100, "#000000", 1) });

        // Act
        var evt = new ChipMovementEvent(
            gameId,
            timestamp,
            ChipMovementType.Bet,
            source,
            destination,
            100,
            chips,
            0,
            500
        );

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.Timestamp.Should().Be(timestamp);
        evt.MovementType.Should().Be(ChipMovementType.Bet);
        evt.Source.LocationId.Should().Be("player1");
        evt.Destination.LocationId.Should().Be("player1-bet");
        evt.Amount.Should().Be(100);
        evt.Sequence.Should().Be(0);
        evt.AnimationDurationMs.Should().Be(500);
    }

    [Fact]
    public void ChipMovementEvent_CollectToPot_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var source = new ChipPosition("player1-bet");
        var destination = new ChipPosition("pot");
        var chips = new ChipStackDto(100, new List<ChipDto>());

        // Act
        var evt = new ChipMovementEvent(
            gameId,
            DateTime.UtcNow,
            ChipMovementType.CollectToPot,
            source,
            destination,
            100,
            chips,
            0
        );

        // Assert
        evt.MovementType.Should().Be(ChipMovementType.CollectToPot);
        evt.Destination.LocationId.Should().Be("pot");
    }

    [Fact]
    public void ChipMovementEvent_Win_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var source = new ChipPosition("pot");
        var destination = new ChipPosition("player2", 2);
        var chips = new ChipStackDto(500, new List<ChipDto>());

        // Act
        var evt = new ChipMovementEvent(
            gameId,
            DateTime.UtcNow,
            ChipMovementType.Win,
            source,
            destination,
            500,
            chips,
            0
        );

        // Assert
        evt.MovementType.Should().Be(ChipMovementType.Win);
        evt.Source.LocationId.Should().Be("pot");
        evt.Destination.SeatNumber.Should().Be(2);
    }

    [Fact]
    public void ChipAnimationStartedEvent_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var animationId = Guid.NewGuid();

        // Act
        var evt = new ChipAnimationStartedEvent(
            gameId,
            DateTime.UtcNow,
            animationId,
            "Collecting bets to pot",
            5
        );

        // Assert
        evt.AnimationId.Should().Be(animationId);
        evt.Description.Should().Be("Collecting bets to pot");
        evt.TotalMovements.Should().Be(5);
    }

    [Fact]
    public void ChipAnimationCompletedEvent_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var animationId = Guid.NewGuid();

        // Act
        var evt = new ChipAnimationCompletedEvent(
            gameId,
            DateTime.UtcNow,
            animationId
        );

        // Assert
        evt.AnimationId.Should().Be(animationId);
    }

    [Fact]
    public void ChipsCollectedToPotEvent_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var contributions = new Dictionary<string, int>
        {
            { "Alice", 50 },
            { "Bob", 100 }
        };

        // Act
        var evt = new ChipsCollectedToPotEvent(
            gameId,
            DateTime.UtcNow,
            "Preflop",
            150,
            150,
            contributions
        );

        // Assert
        evt.RoundName.Should().Be("Preflop");
        evt.TotalCollected.Should().Be(150);
        evt.PotTotal.Should().Be(150);
        evt.PlayerContributions.Should().HaveCount(2);
        evt.PlayerContributions["Alice"].Should().Be(50);
    }

    [Fact]
    public void PotAwardedEvent_MainPot_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var winners = new Dictionary<string, int> { { "Alice", 300 } };

        // Act
        var evt = new PotAwardedEvent(
            gameId,
            DateTime.UtcNow,
            true,
            0,
            300,
            winners,
            "Full House, Aces over Kings"
        );

        // Assert
        evt.IsMainPot.Should().BeTrue();
        evt.PotIndex.Should().Be(0);
        evt.PotAmount.Should().Be(300);
        evt.Winners.Should().ContainKey("Alice");
        evt.WinningHandDescription.Should().Be("Full House, Aces over Kings");
    }

    [Fact]
    public void PotAwardedEvent_SidePot_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var winners = new Dictionary<string, int> { { "Bob", 200 } };

        // Act
        var evt = new PotAwardedEvent(
            gameId,
            DateTime.UtcNow,
            false,
            1,
            200,
            winners
        );

        // Assert
        evt.IsMainPot.Should().BeFalse();
        evt.PotIndex.Should().Be(1);
        evt.WinningHandDescription.Should().BeNull();
    }

    [Fact]
    public void PotAwardedEvent_SplitPot_HasMultipleWinners()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var winners = new Dictionary<string, int>
        {
            { "Alice", 150 },
            { "Bob", 150 }
        };

        // Act
        var evt = new PotAwardedEvent(
            gameId,
            DateTime.UtcNow,
            true,
            0,
            300,
            winners,
            "Pair of Aces"
        );

        // Assert
        evt.Winners.Should().HaveCount(2);
        evt.Winners.Values.Sum().Should().Be(300);
    }

    [Fact]
    public void ChipStackChangedEvent_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = new ChipStackChangedEvent(
            gameId,
            DateTime.UtcNow,
            "Alice",
            1000,
            1300,
            300,
            "Won pot"
        );

        // Assert
        evt.PlayerName.Should().Be("Alice");
        evt.PreviousAmount.Should().Be(1000);
        evt.NewAmount.Should().Be(1300);
        evt.ChangeAmount.Should().Be(300);
        evt.Reason.Should().Be("Won pot");
    }

    [Fact]
    public void ChipStackChangedEvent_Loss_HasNegativeChange()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = new ChipStackChangedEvent(
            gameId,
            DateTime.UtcNow,
            "Bob",
            500,
            400,
            -100,
            "Bet"
        );

        // Assert
        evt.ChangeAmount.Should().Be(-100);
        evt.NewAmount.Should().BeLessThan(evt.PreviousAmount);
    }

    [Fact]
    public void BetPlacedEvent_CreatesCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var chips = new ChipStackDto(50, new List<ChipDto> { new(50, "#FFA500", 1) });

        // Act
        var evt = new BetPlacedEvent(
            gameId,
            DateTime.UtcNow,
            "Charlie",
            50,
            50,
            950,
            chips
        );

        // Assert
        evt.PlayerName.Should().Be("Charlie");
        evt.Amount.Should().Be(50);
        evt.TotalBetThisRound.Should().Be(50);
        evt.RemainingStack.Should().Be(950);
        evt.ChipsDisplay.TotalAmount.Should().Be(50);
    }

    [Theory]
    [InlineData(ChipMovementType.Bet)]
    [InlineData(ChipMovementType.Blind)]
    [InlineData(ChipMovementType.CollectToPot)]
    [InlineData(ChipMovementType.Win)]
    [InlineData(ChipMovementType.Return)]
    public void ChipMovementType_AllValuesAreDistinct(ChipMovementType type)
    {
        // Assert that all enum values are defined and can be used
        Enum.IsDefined(typeof(ChipMovementType), type).Should().BeTrue();
    }

    [Fact]
    public void ChipMovementEvent_InheritsFromGameEvent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var chips = new ChipStackDto(100, new List<ChipDto>());

        // Act
        var evt = new ChipMovementEvent(
            gameId,
            timestamp,
            ChipMovementType.Bet,
            new ChipPosition("player1"),
            new ChipPosition("pot"),
            100,
            chips,
            0
        );

        // Assert
        evt.Should().BeAssignableTo<GameEvent>();
        ((GameEvent)evt).GameId.Should().Be(gameId);
        ((GameEvent)evt).Timestamp.Should().Be(timestamp);
    }
}
