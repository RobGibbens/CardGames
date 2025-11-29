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
