using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Events;

/// <summary>
/// Base type for all game events.
/// </summary>
public abstract record GameEvent(
    Guid GameId,
    DateTime Timestamp);

/// <summary>
/// Event raised when a player joins a game.
/// </summary>
public record PlayerJoinedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int ChipStack) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player leaves a game.
/// </summary>
public record PlayerLeftEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a new hand starts.
/// </summary>
public record HandStartedEvent(
    Guid GameId,
    DateTime Timestamp,
    int HandNumber,
    int DealerPosition,
    int SmallBlind,
    int BigBlind) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when blinds or antes are posted.
/// </summary>
public record BlindsPostedEvent(
    Guid GameId,
    DateTime Timestamp,
    IReadOnlyList<BettingActionDto> BlindActions) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when hole cards are dealt to a player.
/// </summary>
public record HoleCardsDealtEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    IReadOnlyList<CardDto> Cards) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when community cards are dealt (flop, turn, river).
/// </summary>
public record CommunityCardsDealtEvent(
    Guid GameId,
    DateTime Timestamp,
    string StreetName,
    IReadOnlyList<CardDto> Cards,
    IReadOnlyList<CardDto> AllCommunityCards) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player takes a betting action.
/// </summary>
public record BettingActionEvent(
    Guid GameId,
    DateTime Timestamp,
    BettingActionDto Action,
    int PotAfterAction) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when it's a player's turn to act.
/// </summary>
public record PlayerTurnEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    AvailableActionsDto AvailableActions) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when the game state changes.
/// </summary>
public record GameStateChangedEvent(
    Guid GameId,
    DateTime Timestamp,
    GameState PreviousState,
    GameState NewState) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a hand completes and pot is awarded.
/// </summary>
public record HandCompletedEvent(
    Guid GameId,
    DateTime Timestamp,
    int HandNumber,
    IReadOnlyList<string> Winners,
    string WinningDescription,
    IReadOnlyDictionary<string, int> Payouts,
    bool WonByFold) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player shows their cards at showdown.
/// </summary>
public record PlayerShowedCardsEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    IReadOnlyList<CardDto> Cards,
    HandDto Hand) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a game ends.
/// </summary>
public record GameEndedEvent(
    Guid GameId,
    DateTime Timestamp,
    string? WinnerName,
    int TotalHandsPlayed) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a seat becomes available at a table.
/// </summary>
public record SeatAvailableEvent(
    Guid TableId,
    DateTime Timestamp,
    int AvailableSeats,
    int MaxSeats,
    int OccupiedSeats,
    int WaitingListCount) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player's position in the waiting list changes.
/// </summary>
public record WaitingListPositionChangedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int OldPosition,
    int NewPosition) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player is notified that a seat is available.
/// </summary>
public record SeatOfferEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int SeatNumber,
    DateTime OfferExpiresAt) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when the seat status of a table changes (for lobby updates).
/// </summary>
public record TableSeatStatusChangedEvent(
    Guid TableId,
    DateTime Timestamp,
    int OccupiedSeats,
    int MaxSeats,
    int WaitingListCount) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player joins the waiting list.
/// </summary>
public record PlayerJoinedWaitingListEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int Position,
    int WaitingListCount) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player leaves the waiting list.
/// </summary>
public record PlayerLeftWaitingListEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int WaitingListCount) : GameEvent(TableId, Timestamp);

/// <summary>
/// Specifies the type of card being dealt for animation purposes.
/// </summary>
public enum DealCardType
{
    /// <summary>A hole card dealt face down to a player.</summary>
    HoleCard,
    
    /// <summary>A face-up card dealt to a player (stud games).</summary>
    FaceUpCard,
    
    /// <summary>A community card visible to all players.</summary>
    CommunityCard,
    
    /// <summary>A burn card discarded before dealing community cards.</summary>
    BurnCard
}

/// <summary>
/// Event raised when a single card is dealt (for client animation).
/// Clients receive this event to animate card dealing one card at a time.
/// </summary>
public record DealCardEvent(
    Guid GameId,
    DateTime Timestamp,
    string Recipient,
    CardDto? Card,
    DealCardType CardType,
    int DealSequence,
    bool IsFaceDown) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when dealing for a phase begins (for animation timing).
/// </summary>
public record DealingStartedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PhaseName,
    int TotalCardsToBeDealt,
    int? ReplaySeed) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when dealing for a phase completes (for animation timing).
/// </summary>
public record DealingCompletedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PhaseName) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when showdown begins.
/// </summary>
public record ShowdownStartedEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    int HandNumber,
    IReadOnlyList<string> EligiblePlayers,
    string? FirstToReveal,
    bool HadAllInAction) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player reveals their cards at showdown.
/// </summary>
public record PlayerRevealedCardsEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    string PlayerName,
    IReadOnlyList<CardDto>? Cards,
    HandDto? Hand,
    bool WasForcedReveal,
    int RevealOrder) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player mucks (doesn't show) their cards at showdown.
/// </summary>
public record PlayerMuckedCardsEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    string PlayerName,
    bool WasAllowedToMuck) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when it's a player's turn to reveal or muck at showdown.
/// </summary>
public record ShowdownTurnEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    string PlayerName,
    bool CanMuck,
    bool MustShow,
    string? CurrentBestHand) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when the showdown is complete and winners are determined.
/// </summary>
public record ShowdownCompletedEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    int HandNumber,
    IReadOnlyList<string> Winners,
    IReadOnlyDictionary<string, int> Payouts,
    IReadOnlyList<ShowdownRevealDto> FinalReveals) : GameEvent(GameId, Timestamp);

/// <summary>
/// DTO for showdown reveal included in events.
/// </summary>
public record ShowdownRevealDto(
    string PlayerName,
    string Status,
    IReadOnlyList<CardDto>? Cards,
    HandDto? Hand,
    int? RevealOrder);

/// <summary>
/// Event raised when a winner is determined and announced.
/// </summary>
public record WinnerAnnouncementEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    int HandNumber,
    IReadOnlyList<WinnerDetailDto> Winners,
    int TotalPotDistributed,
    bool IsSplitPot,
    bool WonByFold,
    string Summary) : GameEvent(GameId, Timestamp);

/// <summary>
/// DTO for winner details in winner announcement events.
/// </summary>
public record WinnerDetailDto(
    string PlayerName,
    HandDto? Hand,
    IReadOnlyList<CardDto>? WinningCards,
    IReadOnlyList<CardDto>? HoleCards,
    int AmountWon,
    bool IsTie,
    int PotIndex);

/// <summary>
/// Event raised when an all-in showdown board run-out begins.
/// </summary>
public record AllInBoardRunOutStartedEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    int HandNumber,
    IReadOnlyList<string> AllInPlayers,
    IReadOnlyList<CardDto> CurrentCommunityCards,
    int RemainingCardsToReveal) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when community cards are revealed during all-in run-out.
/// </summary>
public record AllInBoardCardRevealedEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    CardDto Card,
    IReadOnlyList<CardDto> AllCommunityCards,
    string StreetName,
    int Sequence) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player's hand is automatically revealed (winner, all-in, etc.).
/// </summary>
public record AutoRevealEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid ShowdownId,
    string PlayerName,
    IReadOnlyList<CardDto> Cards,
    HandDto Hand,
    string Reason) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a showdown animation sequence is ready for playback.
/// </summary>
public record ShowdownAnimationReadyEvent(
    Guid GameId,
    DateTime Timestamp,
    Guid AnimationId,
    Guid ShowdownId,
    IReadOnlyList<ShowdownAnimationStepEventDto> Steps,
    int TotalDurationMs) : GameEvent(GameId, Timestamp);

/// <summary>
/// DTO for animation steps included in showdown animation events.
/// </summary>
public record ShowdownAnimationStepEventDto(
    string AnimationType,
    int Sequence,
    string? PlayerName,
    IReadOnlyList<CardDto>? Cards,
    HandDto? Hand,
    int? Amount,
    int DurationMs,
    string? Message);

#region Seat Events

/// <summary>
/// Event raised when a seat is taken (player sits down).
/// </summary>
public record SeatTakenEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName,
    int ChipStack) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a seat is freed (player stands up or leaves).
/// </summary>
public record SeatFreedEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName,
    int? CashoutAmount = null) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a seat is reserved for a player (pending buy-in).
/// </summary>
public record SeatReservedEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName,
    DateTime ReservedUntil) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a seat reservation expires.
/// </summary>
public record SeatReservationExpiredEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player sits out (going away from the table).
/// </summary>
public record PlayerSatOutEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player sits back in (returns to active play).
/// </summary>
public record PlayerSatBackInEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player requests a seat change.
/// </summary>
public record SeatChangeRequestedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int CurrentSeat,
    int DesiredSeat) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a seat change is completed.
/// </summary>
public record SeatChangeCompletedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int OldSeat,
    int NewSeat) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a seat change request is cancelled (seat no longer available).
/// </summary>
public record SeatChangeCancelledEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    int DesiredSeat,
    string Reason) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player completes buy-in and takes a seat.
/// </summary>
public record PlayerBoughtInEvent(
    Guid TableId,
    DateTime Timestamp,
    int SeatNumber,
    string PlayerName,
    int BuyInAmount) : GameEvent(TableId, Timestamp);

#endregion

#region Dealer Button and Blinds Events

/// <summary>
/// Event raised when the dealer button position changes.
/// </summary>
public record DealerButtonMovedEvent(
    Guid GameId,
    DateTime Timestamp,
    int PreviousPosition,
    int NewPosition,
    string? PreviousDealerName,
    string? NewDealerName,
    bool IsDeadButton,
    int HandNumber) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a single blind is posted.
/// </summary>
public record BlindPostedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int SeatNumber,
    BlindTypeDto BlindType,
    int Amount,
    bool IsDead) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when an ante is posted by a player.
/// </summary>
public record AntePostedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int SeatNumber,
    int Amount) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when all antes have been collected.
/// </summary>
public record AntesCollectedEvent(
    Guid GameId,
    DateTime Timestamp,
    IReadOnlyList<AntePostedEvent> Antes,
    int TotalCollected) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player has missed blinds recorded.
/// </summary>
public record MissedBlindRecordedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    bool MissedSmallBlind,
    bool MissedBigBlind,
    int TotalMissedAmount,
    int HandNumber) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player posts missed blinds upon returning.
/// </summary>
public record MissedBlindsPostedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int SeatNumber,
    int DeadBlindAmount,
    int LiveBlindAmount,
    int TotalAmount) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a dead button situation is detected.
/// </summary>
public record DeadButtonEvent(
    Guid GameId,
    DateTime Timestamp,
    int ButtonPosition,
    string Reason) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event containing the current blind level information.
/// </summary>
public record BlindLevelInfoEvent(
    Guid GameId,
    DateTime Timestamp,
    int SmallBlind,
    int BigBlind,
    int Ante,
    int Level,
    TimeSpan? TimeUntilNextLevel) : GameEvent(GameId, Timestamp);

#endregion

#region Timer Events

/// <summary>
/// Event raised when a player's turn timer starts.
/// </summary>
public record TurnTimerStartedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int DurationSeconds,
    int TimeBankRemaining) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised every second while a player's timer is running.
/// </summary>
public record TurnTimerTickEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int SecondsRemaining) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player's timer is about to expire.
/// </summary>
public record TurnTimerWarningEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int SecondsRemaining) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player's timer expires and a default action is taken.
/// </summary>
public record TurnTimerExpiredEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    string DefaultAction) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player activates their time bank.
/// </summary>
public record TimeBankActivatedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName,
    int SecondsAdded,
    int TimeBankRemaining) : GameEvent(GameId, Timestamp);

/// <summary>
/// Event raised when a player's timer is stopped (player took action).
/// </summary>
public record TurnTimerStoppedEvent(
    Guid GameId,
    DateTime Timestamp,
    string PlayerName) : GameEvent(GameId, Timestamp);

#endregion
