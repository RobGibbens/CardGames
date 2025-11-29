using System;
using System.Collections.Generic;

namespace CardGames.Poker.Betting;

/// <summary>
/// Interface for the betting engine that manages betting rounds and enforces rules.
/// </summary>
public interface IBettingEngine
{
    /// <summary>
    /// Gets whether a betting round is currently in progress.
    /// </summary>
    bool IsRoundInProgress { get; }

    /// <summary>
    /// Gets whether the current betting round is complete.
    /// </summary>
    bool IsRoundComplete { get; }

    /// <summary>
    /// Gets the current player who needs to act.
    /// </summary>
    PokerPlayer CurrentPlayer { get; }

    /// <summary>
    /// Gets the current bet amount that players need to match.
    /// </summary>
    int CurrentBet { get; }

    /// <summary>
    /// Gets the total pot amount.
    /// </summary>
    int TotalPot { get; }

    /// <summary>
    /// Gets the actions taken in the current betting round.
    /// </summary>
    IReadOnlyList<BettingAction> CurrentRoundActions { get; }

    /// <summary>
    /// Gets the limit strategy being used.
    /// </summary>
    ILimitStrategy LimitStrategy { get; }

    /// <summary>
    /// Event raised when a betting engine event occurs.
    /// </summary>
    event Action<BettingEngineEvent> OnEvent;

    /// <summary>
    /// Starts a new betting round.
    /// </summary>
    /// <param name="roundName">The name of the betting round (e.g., "Preflop", "Flop").</param>
    /// <param name="initialBet">The initial bet amount (e.g., big blind amount for preflop).</param>
    /// <param name="forcedBetPlayerIndex">Index of the player who posted the forced bet (-1 if none).</param>
    void StartRound(string roundName, int initialBet = 0, int forcedBetPlayerIndex = -1);

    /// <summary>
    /// Gets the available actions for the current player.
    /// </summary>
    /// <returns>The available actions for the current player.</returns>
    AvailableActions GetAvailableActions();

    /// <summary>
    /// Gets the available actions for a specific player.
    /// </summary>
    /// <param name="player">The player to get available actions for.</param>
    /// <returns>The available actions for the specified player.</returns>
    AvailableActions GetAvailableActionsForPlayer(PokerPlayer player);

    /// <summary>
    /// Processes a betting action from the current player.
    /// </summary>
    /// <param name="actionType">The type of action being taken.</param>
    /// <param name="amount">The amount for bet/raise actions.</param>
    /// <returns>The result of processing the action.</returns>
    BettingEngineResult ProcessAction(BettingActionType actionType, int amount = 0);

    /// <summary>
    /// Processes the default action when a player times out.
    /// The default action is check if available, otherwise fold.
    /// </summary>
    /// <returns>The result of processing the default action.</returns>
    BettingEngineResult ProcessDefaultAction();

    /// <summary>
    /// Validates whether an action is valid for the current player.
    /// </summary>
    /// <param name="actionType">The type of action to validate.</param>
    /// <param name="amount">The amount for bet/raise actions.</param>
    /// <returns>True if the action is valid, false otherwise.</returns>
    bool IsValidAction(BettingActionType actionType, int amount = 0);

    /// <summary>
    /// Gets the number of players still in the hand (not folded).
    /// </summary>
    int PlayersInHand { get; }

    /// <summary>
    /// Gets the number of players who can still act (not folded, not all-in).
    /// </summary>
    int ActivePlayers { get; }

    /// <summary>
    /// Resets player bets for a new betting round.
    /// </summary>
    void ResetPlayerBets();
}

/// <summary>
/// Result of processing a betting action.
/// </summary>
public class BettingEngineResult
{
    /// <summary>
    /// Whether the action was processed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The action that was taken.
    /// </summary>
    public BettingAction Action { get; init; }

    /// <summary>
    /// Error message if the action failed.
    /// </summary>
    public string ErrorMessage { get; init; }

    /// <summary>
    /// Whether the betting round is complete after this action.
    /// </summary>
    public bool RoundComplete { get; init; }

    /// <summary>
    /// Whether the hand is complete (only one player remaining).
    /// </summary>
    public bool HandComplete { get; init; }

    /// <summary>
    /// The total pot amount after the action.
    /// </summary>
    public int PotAfterAction { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static BettingEngineResult Successful(BettingAction action, int potAfterAction, bool roundComplete, bool handComplete = false)
    {
        return new BettingEngineResult
        {
            Success = true,
            Action = action,
            PotAfterAction = potAfterAction,
            RoundComplete = roundComplete,
            HandComplete = handComplete
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static BettingEngineResult Failed(string errorMessage)
    {
        return new BettingEngineResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
