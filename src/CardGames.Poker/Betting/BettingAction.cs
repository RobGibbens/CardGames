namespace CardGames.Poker.Betting;

/// <summary>
/// Represents the possible betting actions a player can take during a betting round.
/// </summary>
public enum BettingActionType
{
    /// <summary>Pass the action without betting when no bet has been made.</summary>
    Check,
    
    /// <summary>Make an initial bet in the current betting round.</summary>
    Bet,
    
    /// <summary>Match the current highest bet.</summary>
    Call,
    
    /// <summary>Increase the current bet amount.</summary>
    Raise,
    
    /// <summary>Give up the hand and forfeit any chips already in the pot.</summary>
    Fold,
    
    /// <summary>Bet all remaining chips.</summary>
    AllIn,
    
    /// <summary>Post ante or blind (forced bet).</summary>
    Post
}

/// <summary>
/// Represents a betting action taken by a player.
/// </summary>
public class BettingAction
{
    public string PlayerName { get; }
    public BettingActionType ActionType { get; }
    public int Amount { get; }

    public BettingAction(string playerName, BettingActionType actionType, int amount = 0)
    {
        PlayerName = playerName;
        ActionType = actionType;
        Amount = amount;
    }

    public override string ToString()
    {
        return ActionType switch
        {
            BettingActionType.Check => $"{PlayerName} checks",
            BettingActionType.Bet => $"{PlayerName} bets {Amount}",
            BettingActionType.Call => $"{PlayerName} calls {Amount}",
            BettingActionType.Raise => $"{PlayerName} raises to {Amount}",
            BettingActionType.Fold => $"{PlayerName} folds",
            BettingActionType.AllIn => $"{PlayerName} is all-in for {Amount}",
            BettingActionType.Post => $"{PlayerName} posts {Amount}",
            _ => $"{PlayerName} unknown action"
        };
    }
}
