using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a betting action taken by a player.
/// </summary>
public record BettingActionDto(
    string PlayerName,
    BettingActionType ActionType,
    int Amount,
    DateTime Timestamp);

/// <summary>
/// Represents the available actions for the current player.
/// </summary>
public record AvailableActionsDto(
    string PlayerName,
    bool CanCheck,
    bool CanBet,
    bool CanCall,
    bool CanRaise,
    bool CanFold,
    bool CanAllIn,
    int MinBet,
    int MaxBet,
    int CallAmount,
    int MinRaise,
    int MaxRaise);
