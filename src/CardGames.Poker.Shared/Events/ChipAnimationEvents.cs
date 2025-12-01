using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Events;

/// <summary>
/// Specifies the type of chip movement for animation purposes.
/// </summary>
public enum ChipMovementType
{
    /// <summary>Chips moving from player stack to betting area (bet, raise, call).</summary>
    Bet,
    
    /// <summary>Chips moving from player stack for a blind or ante.</summary>
    Blind,
    
    /// <summary>Chips gathering from betting areas to pot.</summary>
    CollectToPot,
    
    /// <summary>Chips distributing from pot to winning player(s).</summary>
    Win,
    
    /// <summary>Chips returned to player (uncalled bet).</summary>
    Return
}

/// <summary>
/// Represents a position on the table for chip animations.
/// </summary>
public record ChipPosition(
    /// <summary>The identifier for this position (player name, "pot", "table-center", etc.).</summary>
    string LocationId,
    
    /// <summary>Optional seat number if the location is a player seat.</summary>
    int? SeatNumber = null);

/// <summary>
/// Represents a single chip with a specific denomination.
/// </summary>
public record ChipDto(
    /// <summary>The value of this chip denomination.</summary>
    int Denomination,
    
    /// <summary>The color associated with this denomination.</summary>
    string Color,
    
    /// <summary>The number of chips of this denomination.</summary>
    int Count);

/// <summary>
/// Represents a stack of chips broken down by denomination.
/// </summary>
public record ChipStackDto(
    /// <summary>The total value of the chip stack.</summary>
    int TotalAmount,
    
    /// <summary>The chips broken down by denomination.</summary>
    IReadOnlyList<ChipDto> Chips);

/// <summary>
/// Event raised when chips should animate from one location to another.
/// </summary>
public record ChipMovementEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>The type of chip movement.</summary>
    ChipMovementType MovementType,
    /// <summary>Where the chips are coming from.</summary>
    ChipPosition Source,
    /// <summary>Where the chips are going to.</summary>
    ChipPosition Destination,
    /// <summary>The total amount being moved.</summary>
    int Amount,
    /// <summary>The chips broken down by denomination for visual display.</summary>
    ChipStackDto Chips,
    /// <summary>Sequence number for ordering multiple animations.</summary>
    int Sequence,
    /// <summary>Duration hint for the animation in milliseconds.</summary>
    int AnimationDurationMs = 500) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a chip animation sequence starts.
/// </summary>
public record ChipAnimationStartedEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>Unique identifier for this animation sequence.</summary>
    Guid AnimationId,
    /// <summary>Description of what the animation represents.</summary>
    string Description,
    /// <summary>Total number of movements in this sequence.</summary>
    int TotalMovements) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a chip animation sequence completes.
/// </summary>
public record ChipAnimationCompletedEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>The animation sequence that completed.</summary>
    Guid AnimationId) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when chips are collected from all betting areas to the pot.
/// </summary>
public record ChipsCollectedToPotEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>The round name (Preflop, Flop, Turn, River).</summary>
    string RoundName,
    /// <summary>Total amount collected to the pot.</summary>
    int TotalCollected,
    /// <summary>New pot total after collection.</summary>
    int PotTotal,
    /// <summary>Individual collections by player.</summary>
    IReadOnlyDictionary<string, int> PlayerContributions) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when the pot is awarded to winner(s).
/// </summary>
public record PotAwardedEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>Whether this is the main pot or a side pot.</summary>
    bool IsMainPot,
    /// <summary>Index of the side pot (0 for main pot).</summary>
    int PotIndex,
    /// <summary>Total amount in this pot.</summary>
    int PotAmount,
    /// <summary>Winners and their share of this pot.</summary>
    IReadOnlyDictionary<string, int> Winners,
    /// <summary>Description of the winning hand if applicable.</summary>
    string? WinningHandDescription = null) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player's chip stack changes.
/// </summary>
public record ChipStackChangedEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>The player whose stack changed.</summary>
    string PlayerName,
    /// <summary>The previous stack amount.</summary>
    int PreviousAmount,
    /// <summary>The new stack amount.</summary>
    int NewAmount,
    /// <summary>The change amount (positive for gain, negative for loss).</summary>
    int ChangeAmount,
    /// <summary>Reason for the change.</summary>
    string Reason) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player places chips in their betting area.
/// </summary>
public record BetPlacedEvent(
    Guid GameId,
    DateTime Timestamp,
    /// <summary>The player placing the bet.</summary>
    string PlayerName,
    /// <summary>The bet amount.</summary>
    int Amount,
    /// <summary>The total amount the player has bet this round.</summary>
    int TotalBetThisRound,
    /// <summary>The player's remaining stack after the bet.</summary>
    int RemainingStack,
    /// <summary>The chips to display in the betting area.</summary>
    ChipStackDto ChipsDisplay) : GameEvent(GameId, Timestamp);
