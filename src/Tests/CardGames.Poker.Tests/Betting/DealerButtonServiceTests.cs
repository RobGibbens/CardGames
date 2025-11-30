using System.Collections.Generic;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class DealerButtonServiceTests
{
    private readonly DealerButtonService _service = new();

    #region InitializeButton Tests

    [Fact]
    public void InitializeButton_WithTwoPlayers_ReturnsFirstSeat()
    {
        var occupiedSeats = new List<int> { 0, 1 };

        var result = _service.InitializeButton(occupiedSeats);

        result.ButtonPosition.Should().Be(0);
        result.IsDeadButton.Should().BeFalse();
        result.HandNumber.Should().Be(0);
    }

    [Fact]
    public void InitializeButton_WithNonConsecutiveSeats_ReturnsLowestSeat()
    {
        var occupiedSeats = new List<int> { 3, 7 };

        var result = _service.InitializeButton(occupiedSeats);

        result.ButtonPosition.Should().Be(3);
    }

    [Fact]
    public void InitializeButton_WithLessThanTwoPlayers_ThrowsException()
    {
        var occupiedSeats = new List<int> { 0 };

        var act = () => _service.InitializeButton(occupiedSeats);

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*at least 2*");
    }

    #endregion

    #region MoveButtonClockwise Tests

    [Fact]
    public void MoveButtonClockwise_FromFirstToSecondSeat_ReturnsSecondSeat()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };

        var result = _service.MoveButtonClockwise(0, occupiedSeats, 10);

        result.Should().Be(1);
    }

    [Fact]
    public void MoveButtonClockwise_FromLastSeat_WrapsToFirst()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };

        var result = _service.MoveButtonClockwise(2, occupiedSeats, 10);

        result.Should().Be(0);
    }

    [Fact]
    public void MoveButtonClockwise_WithNonConsecutiveSeats_SkipsEmptySeats()
    {
        var occupiedSeats = new List<int> { 0, 3, 7 };

        var result = _service.MoveButtonClockwise(0, occupiedSeats, 10);

        result.Should().Be(3);
    }

    [Fact]
    public void MoveButtonClockwise_HeadsUp_AlternatesBetweenPlayers()
    {
        var occupiedSeats = new List<int> { 2, 5 };

        var result1 = _service.MoveButtonClockwise(2, occupiedSeats, 10);
        var result2 = _service.MoveButtonClockwise(5, occupiedSeats, 10);

        result1.Should().Be(5);
        result2.Should().Be(2);
    }

    #endregion

    #region GetSmallBlindPosition Tests

    [Fact]
    public void GetSmallBlindPosition_HeadsUp_ReturnsDealerPosition()
    {
        var occupiedSeats = new List<int> { 0, 1 };
        var buttonPosition = 0;

        var result = _service.GetSmallBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(0); // Dealer is small blind in heads-up
    }

    [Fact]
    public void GetSmallBlindPosition_ThreePlayers_ReturnsLeftOfDealer()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };
        var buttonPosition = 0;

        var result = _service.GetSmallBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(1); // First player left of dealer
    }

    [Fact]
    public void GetSmallBlindPosition_WithGaps_SkipsEmptySeats()
    {
        var occupiedSeats = new List<int> { 0, 3, 7 };
        var buttonPosition = 0;

        var result = _service.GetSmallBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(3);
    }

    #endregion

    #region GetBigBlindPosition Tests

    [Fact]
    public void GetBigBlindPosition_HeadsUp_ReturnsNonDealer()
    {
        var occupiedSeats = new List<int> { 0, 1 };
        var buttonPosition = 0;

        var result = _service.GetBigBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(1); // Non-dealer is big blind in heads-up
    }

    [Fact]
    public void GetBigBlindPosition_ThreePlayers_ReturnsTwoLeftOfDealer()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };
        var buttonPosition = 0;

        var result = _service.GetBigBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(2); // Second player left of dealer
    }

    [Fact]
    public void GetBigBlindPosition_WithWrapAround_HandlesCorrectly()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };
        var buttonPosition = 1;

        var result = _service.GetBigBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(0); // Wraps around from position 2 to 0
    }

    #endregion

    #region GetFirstToActPreFlop Tests

    [Fact]
    public void GetFirstToActPreFlop_HeadsUp_ReturnsDealer()
    {
        var occupiedSeats = new List<int> { 0, 1 };
        var buttonPosition = 0;

        var result = _service.GetFirstToActPreFlop(buttonPosition, occupiedSeats, 10);

        result.Should().Be(0); // Dealer acts first pre-flop in heads-up
    }

    [Fact]
    public void GetFirstToActPreFlop_ThreePlayers_ReturnsUTG()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };
        var buttonPosition = 0;

        var result = _service.GetFirstToActPreFlop(buttonPosition, occupiedSeats, 10);

        result.Should().Be(0); // Player after big blind (wraps around)
    }

    [Fact]
    public void GetFirstToActPreFlop_FourPlayers_ReturnsUTG()
    {
        var occupiedSeats = new List<int> { 0, 1, 2, 3 };
        var buttonPosition = 0;

        var result = _service.GetFirstToActPreFlop(buttonPosition, occupiedSeats, 10);

        result.Should().Be(3); // Player after big blind
    }

    #endregion

    #region GetFirstToActPostFlop Tests

    [Fact]
    public void GetFirstToActPostFlop_ReturnsFirstActiveLeftOfDealer()
    {
        var activeSeatNumbers = new List<int> { 0, 1, 2 };
        var buttonPosition = 0;

        var result = _service.GetFirstToActPostFlop(buttonPosition, activeSeatNumbers, 10);

        result.Should().Be(1); // First active player left of dealer
    }

    [Fact]
    public void GetFirstToActPostFlop_WithFolds_SkipsFoldedPlayers()
    {
        var activeSeatNumbers = new List<int> { 0, 3 }; // Players 1 and 2 folded
        var buttonPosition = 0;

        var result = _service.GetFirstToActPostFlop(buttonPosition, activeSeatNumbers, 10);

        result.Should().Be(3);
    }

    #endregion

    #region HandleSeatChange Tests

    [Fact]
    public void HandleSeatChange_ButtonStillOccupied_NoChange()
    {
        var currentState = new DealerButtonState
        {
            ButtonPosition = 0,
            IsDeadButton = false,
            HandNumber = 5
        };
        var occupiedSeats = new List<int> { 0, 1, 2 };

        var result = _service.HandleSeatChange(currentState, occupiedSeats, 10);

        result.ButtonPosition.Should().Be(0);
        result.IsDeadButton.Should().BeFalse();
    }

    [Fact]
    public void HandleSeatChange_ButtonOnEmptySeat_SetsDeadButton()
    {
        var currentState = new DealerButtonState
        {
            ButtonPosition = 1,
            IsDeadButton = false,
            HandNumber = 5
        };
        var occupiedSeats = new List<int> { 0, 2, 3 }; // Seat 1 is now empty

        var result = _service.HandleSeatChange(currentState, occupiedSeats, 10);

        result.ButtonPosition.Should().Be(1); // Button stays in place
        result.IsDeadButton.Should().BeTrue();
    }

    #endregion

    #region AdvanceButton Tests

    [Fact]
    public void AdvanceButton_IncrementsHandNumber()
    {
        var currentState = new DealerButtonState
        {
            ButtonPosition = 0,
            IsDeadButton = false,
            HandNumber = 5
        };
        var occupiedSeats = new List<int> { 0, 1, 2 };

        var result = _service.AdvanceButton(currentState, occupiedSeats, 10);

        result.HandNumber.Should().Be(6);
    }

    [Fact]
    public void AdvanceButton_ClearsDeadButtonFlag()
    {
        var currentState = new DealerButtonState
        {
            ButtonPosition = 1,
            IsDeadButton = true,
            HandNumber = 5
        };
        var occupiedSeats = new List<int> { 0, 2, 3 };

        var result = _service.AdvanceButton(currentState, occupiedSeats, 10);

        result.IsDeadButton.Should().BeFalse();
    }

    [Fact]
    public void AdvanceButton_MovesClockwise()
    {
        var currentState = new DealerButtonState
        {
            ButtonPosition = 0,
            IsDeadButton = false,
            HandNumber = 0
        };
        var occupiedSeats = new List<int> { 0, 1, 2 };

        var result = _service.AdvanceButton(currentState, occupiedSeats, 10);

        result.ButtonPosition.Should().Be(1);
    }

    #endregion

    #region Edge Case Tests for Seat 0

    [Fact]
    public void MoveButtonClockwise_ToSeat0_WorksCorrectly()
    {
        // Button at seat 5, next seat clockwise should be seat 0
        var occupiedSeats = new List<int> { 0, 3, 5 };

        var result = _service.MoveButtonClockwise(5, occupiedSeats, 10);

        result.Should().Be(0); // Should wrap around to seat 0
    }

    [Fact]
    public void MoveButtonClockwise_FromSeat0_WorksCorrectly()
    {
        // Button at seat 0, next seat clockwise should be next occupied
        var occupiedSeats = new List<int> { 0, 3, 5 };

        var result = _service.MoveButtonClockwise(0, occupiedSeats, 10);

        result.Should().Be(3);
    }

    [Fact]
    public void GetSmallBlindPosition_WhenDealerAtSeat0_ThreePlayers()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };
        var buttonPosition = 0;

        var result = _service.GetSmallBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(1); // Small blind is next after dealer
    }

    [Fact]
    public void GetBigBlindPosition_WhenDealerAtSeat0_ThreePlayers()
    {
        var occupiedSeats = new List<int> { 0, 1, 2 };
        var buttonPosition = 0;

        var result = _service.GetBigBlindPosition(buttonPosition, occupiedSeats, 10);

        result.Should().Be(2); // Big blind is 2 positions after dealer
    }

    [Fact]
    public void CollectAntes_WithExplicitSeatNumbers_UsesProvidedSeats()
    {
        // Arrange
        var blindService = new BlindPostingService();
        var players = new List<PokerPlayer>
        {
            new("Alice", 1000),
            new("Bob", 1000),
            new("Charlie", 1000)
        };
        var seatNumbers = new List<int> { 3, 5, 8 }; // Non-consecutive seats
        var potManager = new PotManager();

        // Act
        var result = blindService.CollectAntes(players, seatNumbers, 5, potManager);

        // Assert
        result.Success.Should().BeTrue();
        result.PostedAntes.Should().HaveCount(3);
        result.PostedAntes[0].SeatNumber.Should().Be(3);
        result.PostedAntes[1].SeatNumber.Should().Be(5);
        result.PostedAntes[2].SeatNumber.Should().Be(8);
    }

    #endregion
}
