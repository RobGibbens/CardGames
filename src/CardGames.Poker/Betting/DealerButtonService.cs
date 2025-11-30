using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Betting;

/// <summary>
/// Represents the state of the dealer button at a table.
/// </summary>
public class DealerButtonState
{
    /// <summary>
    /// The current seat position of the dealer button.
    /// </summary>
    public int ButtonPosition { get; set; }

    /// <summary>
    /// Indicates if this is a "dead button" situation where the button sits on an empty seat.
    /// </summary>
    public bool IsDeadButton { get; set; }

    /// <summary>
    /// The hand number when this position was set.
    /// </summary>
    public int HandNumber { get; set; }
}

/// <summary>
/// Service for managing dealer button rotation logic.
/// Handles both normal rotation and dead button rules.
/// </summary>
public class DealerButtonService
{
    /// <summary>
    /// Calculates the next dealer button position after a hand completes.
    /// Moves the button clockwise to the next occupied seat.
    /// </summary>
    /// <param name="currentPosition">The current button position.</param>
    /// <param name="occupiedSeats">List of occupied seat numbers in order.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <returns>The new button position.</returns>
    public int MoveButtonClockwise(int currentPosition, IReadOnlyList<int> occupiedSeats, int maxSeats)
    {
        if (occupiedSeats == null || occupiedSeats.Count < 2)
        {
            throw new ArgumentException("At least 2 occupied seats are required to move the button.");
        }

        // Find the next occupied seat clockwise from current position
        var sortedSeats = occupiedSeats.OrderBy(s => s).ToList();
        
        // Find seats that are clockwise from current position
        var nextSeat = sortedSeats.FirstOrDefault(s => s > currentPosition);
        
        if (nextSeat == 0 && !sortedSeats.Contains(0))
        {
            // No seat found clockwise, wrap around to the first seat
            nextSeat = sortedSeats.First();
        }
        else if (nextSeat == 0 && sortedSeats.Contains(0))
        {
            // Seat 0 is occupied and is the next seat
            // Check if there's a seat greater than current
            var seatsAfterCurrent = sortedSeats.Where(s => s > currentPosition).ToList();
            nextSeat = seatsAfterCurrent.Count != 0 ? seatsAfterCurrent.First() : sortedSeats.First();
        }

        return nextSeat;
    }

    /// <summary>
    /// Calculates the button position when a new player joins or leaves,
    /// handling dead button scenarios.
    /// </summary>
    /// <param name="currentState">Current button state.</param>
    /// <param name="occupiedSeats">List of currently occupied seat numbers.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <returns>Updated button state, potentially with dead button flag.</returns>
    public DealerButtonState HandleSeatChange(
        DealerButtonState currentState,
        IReadOnlyList<int> occupiedSeats,
        int maxSeats)
    {
        if (occupiedSeats == null || occupiedSeats.Count < 2)
        {
            return currentState;
        }

        // If the current button position is still occupied, no change needed
        if (occupiedSeats.Contains(currentState.ButtonPosition))
        {
            return new DealerButtonState
            {
                ButtonPosition = currentState.ButtonPosition,
                IsDeadButton = false,
                HandNumber = currentState.HandNumber
            };
        }

        // Button is on an empty seat - this is a dead button scenario
        return new DealerButtonState
        {
            ButtonPosition = currentState.ButtonPosition,
            IsDeadButton = true,
            HandNumber = currentState.HandNumber
        };
    }

    /// <summary>
    /// Determines the small blind position based on button position.
    /// </summary>
    /// <param name="buttonPosition">The current dealer button position.</param>
    /// <param name="occupiedSeats">List of occupied seat numbers.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <param name="isDeadButton">Whether this is a dead button situation.</param>
    /// <returns>The seat position for the small blind.</returns>
    public int GetSmallBlindPosition(
        int buttonPosition,
        IReadOnlyList<int> occupiedSeats,
        int maxSeats,
        bool isDeadButton = false)
    {
        if (occupiedSeats == null || occupiedSeats.Count < 2)
        {
            throw new ArgumentException("At least 2 occupied seats are required.");
        }

        var sortedSeats = occupiedSeats.OrderBy(s => s).ToList();

        // Heads-up: dealer is small blind
        if (occupiedSeats.Count == 2)
        {
            return buttonPosition;
        }

        // Dead button: small blind stays where it was (next active player clockwise from button)
        // Normal: small blind is first active player clockwise from button
        return GetNextOccupiedSeat(buttonPosition, sortedSeats, maxSeats);
    }

    /// <summary>
    /// Determines the big blind position based on button position.
    /// </summary>
    /// <param name="buttonPosition">The current dealer button position.</param>
    /// <param name="occupiedSeats">List of occupied seat numbers.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <param name="isDeadButton">Whether this is a dead button situation.</param>
    /// <returns>The seat position for the big blind.</returns>
    public int GetBigBlindPosition(
        int buttonPosition,
        IReadOnlyList<int> occupiedSeats,
        int maxSeats,
        bool isDeadButton = false)
    {
        if (occupiedSeats == null || occupiedSeats.Count < 2)
        {
            throw new ArgumentException("At least 2 occupied seats are required.");
        }

        var sortedSeats = occupiedSeats.OrderBy(s => s).ToList();

        // Heads-up: non-dealer is big blind
        if (occupiedSeats.Count == 2)
        {
            return GetNextOccupiedSeat(buttonPosition, sortedSeats, maxSeats);
        }

        // Big blind is the second active player clockwise from button
        var smallBlindPosition = GetSmallBlindPosition(buttonPosition, occupiedSeats, maxSeats, isDeadButton);
        return GetNextOccupiedSeat(smallBlindPosition, sortedSeats, maxSeats);
    }

    /// <summary>
    /// Gets the first position to act pre-flop.
    /// </summary>
    /// <param name="buttonPosition">The current dealer button position.</param>
    /// <param name="occupiedSeats">List of occupied seat numbers.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <param name="isDeadButton">Whether this is a dead button situation.</param>
    /// <returns>The seat position of the first player to act.</returns>
    public int GetFirstToActPreFlop(
        int buttonPosition,
        IReadOnlyList<int> occupiedSeats,
        int maxSeats,
        bool isDeadButton = false)
    {
        if (occupiedSeats == null || occupiedSeats.Count < 2)
        {
            throw new ArgumentException("At least 2 occupied seats are required.");
        }

        var sortedSeats = occupiedSeats.OrderBy(s => s).ToList();

        // Heads-up: dealer (small blind) acts first pre-flop
        if (occupiedSeats.Count == 2)
        {
            return buttonPosition;
        }

        // Player left of big blind acts first (UTG position)
        var bigBlindPosition = GetBigBlindPosition(buttonPosition, occupiedSeats, maxSeats, isDeadButton);
        return GetNextOccupiedSeat(bigBlindPosition, sortedSeats, maxSeats);
    }

    /// <summary>
    /// Gets the first position to act post-flop.
    /// </summary>
    /// <param name="buttonPosition">The current dealer button position.</param>
    /// <param name="activeSeatNumbers">List of seat numbers of players still in the hand.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <returns>The seat position of the first player to act.</returns>
    public int GetFirstToActPostFlop(
        int buttonPosition,
        IReadOnlyList<int> activeSeatNumbers,
        int maxSeats)
    {
        if (activeSeatNumbers == null || activeSeatNumbers.Count < 1)
        {
            throw new ArgumentException("At least 1 active seat is required.");
        }

        var sortedSeats = activeSeatNumbers.OrderBy(s => s).ToList();

        // First active player clockwise from dealer
        return GetNextOccupiedSeat(buttonPosition, sortedSeats, maxSeats);
    }

    /// <summary>
    /// Initializes the dealer button for a new game.
    /// </summary>
    /// <param name="occupiedSeats">List of occupied seat numbers.</param>
    /// <returns>Initial button state.</returns>
    public DealerButtonState InitializeButton(IReadOnlyList<int> occupiedSeats)
    {
        if (occupiedSeats == null || occupiedSeats.Count < 2)
        {
            throw new ArgumentException("At least 2 occupied seats are required to start a game.");
        }

        // Button starts at the first occupied seat
        var firstSeat = occupiedSeats.OrderBy(s => s).First();

        return new DealerButtonState
        {
            ButtonPosition = firstSeat,
            IsDeadButton = false,
            HandNumber = 0
        };
    }

    /// <summary>
    /// Advances the button for a new hand.
    /// </summary>
    /// <param name="currentState">Current button state.</param>
    /// <param name="occupiedSeats">List of currently occupied seat numbers.</param>
    /// <param name="maxSeats">Maximum number of seats at the table.</param>
    /// <returns>New button state for the next hand.</returns>
    public DealerButtonState AdvanceButton(
        DealerButtonState currentState,
        IReadOnlyList<int> occupiedSeats,
        int maxSeats)
    {
        var newPosition = MoveButtonClockwise(currentState.ButtonPosition, occupiedSeats, maxSeats);

        return new DealerButtonState
        {
            ButtonPosition = newPosition,
            IsDeadButton = false,
            HandNumber = currentState.HandNumber + 1
        };
    }

    private int GetNextOccupiedSeat(int currentPosition, List<int> sortedSeats, int maxSeats)
    {
        // Find the next occupied seat clockwise
        var seatsAfterCurrent = sortedSeats.Where(s => s > currentPosition).ToList();
        
        if (seatsAfterCurrent.Count != 0)
        {
            return seatsAfterCurrent.First();
        }

        // Wrap around to the beginning
        return sortedSeats.First();
    }
}
