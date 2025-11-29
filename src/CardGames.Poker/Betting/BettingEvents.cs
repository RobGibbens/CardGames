using System;
using System.Collections.Generic;

namespace CardGames.Poker.Betting;

/// <summary>
/// Base class for all betting engine events.
/// </summary>
public abstract class BettingEngineEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a player's turn begins.
/// </summary>
public class TurnStartedEvent : BettingEngineEvent
{
    public string PlayerName { get; }
    public AvailableActions AvailableActions { get; }
    public int TimeoutSeconds { get; }

    public TurnStartedEvent(string playerName, AvailableActions availableActions, int timeoutSeconds)
    {
        PlayerName = playerName;
        AvailableActions = availableActions;
        TimeoutSeconds = timeoutSeconds;
    }
}

/// <summary>
/// Event raised when a player takes an action.
/// </summary>
public class ActionTakenEvent : BettingEngineEvent
{
    public BettingAction Action { get; }
    public int PotAfterAction { get; }
    public bool IsRoundComplete { get; }

    public ActionTakenEvent(BettingAction action, int potAfterAction, bool isRoundComplete)
    {
        Action = action;
        PotAfterAction = potAfterAction;
        IsRoundComplete = isRoundComplete;
    }
}

/// <summary>
/// Event raised when a player's turn times out.
/// </summary>
public class TurnTimedOutEvent : BettingEngineEvent
{
    public string PlayerName { get; }
    public BettingAction DefaultAction { get; }

    public TurnTimedOutEvent(string playerName, BettingAction defaultAction)
    {
        PlayerName = playerName;
        DefaultAction = defaultAction;
    }
}

/// <summary>
/// Event raised when a betting round begins.
/// </summary>
public class BettingRoundStartedEvent : BettingEngineEvent
{
    public string RoundName { get; }
    public int PotAmount { get; }
    public IReadOnlyList<string> ActivePlayers { get; }

    public BettingRoundStartedEvent(string roundName, int potAmount, IReadOnlyList<string> activePlayers)
    {
        RoundName = roundName;
        PotAmount = potAmount;
        ActivePlayers = activePlayers;
    }
}

/// <summary>
/// Event raised when a betting round completes.
/// </summary>
public class BettingRoundCompletedEvent : BettingEngineEvent
{
    public string RoundName { get; }
    public int PotAmount { get; }
    public IReadOnlyList<BettingAction> Actions { get; }
    public bool HandComplete { get; }

    public BettingRoundCompletedEvent(string roundName, int potAmount, IReadOnlyList<BettingAction> actions, bool handComplete)
    {
        RoundName = roundName;
        PotAmount = potAmount;
        Actions = actions;
        HandComplete = handComplete;
    }
}

/// <summary>
/// Event raised when a player's action is invalid.
/// </summary>
public class InvalidActionEvent : BettingEngineEvent
{
    public string PlayerName { get; }
    public BettingActionType AttemptedAction { get; }
    public int AttemptedAmount { get; }
    public string ErrorMessage { get; }

    public InvalidActionEvent(string playerName, BettingActionType attemptedAction, int attemptedAmount, string errorMessage)
    {
        PlayerName = playerName;
        AttemptedAction = attemptedAction;
        AttemptedAmount = attemptedAmount;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Event raised when a player is warned about turn timeout.
/// </summary>
public class TurnWarningEvent : BettingEngineEvent
{
    public string PlayerName { get; }
    public int SecondsRemaining { get; }

    public TurnWarningEvent(string playerName, int secondsRemaining)
    {
        PlayerName = playerName;
        SecondsRemaining = secondsRemaining;
    }
}
