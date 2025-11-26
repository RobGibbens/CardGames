using System;

namespace CardGames.Poker.Betting;

/// <summary>
/// Represents a player in a poker game with chip management capabilities.
/// </summary>
public class PokerPlayer
{
    public string Name { get; }
    public int ChipStack { get; private set; }
    public int CurrentBet { get; private set; }
    public bool HasFolded { get; private set; }
    public bool IsAllIn => ChipStack == 0 && !HasFolded;
    public bool IsActive => !HasFolded && ChipStack > 0;
    public bool CanAct => !HasFolded && !IsAllIn;

    public PokerPlayer(string name, int initialChips)
    {
        if (initialChips < 0)
        {
            throw new ArgumentException("Initial chips cannot be negative", nameof(initialChips));
        }
        Name = name;
        ChipStack = initialChips;
        CurrentBet = 0;
        HasFolded = false;
    }

    /// <summary>
    /// Places chips into the pot. Returns the actual amount placed (may be less if all-in).
    /// </summary>
    public int PlaceBet(int amount)
    {
        var actualAmount = Math.Min(amount, ChipStack);
        ChipStack -= actualAmount;
        CurrentBet += actualAmount;
        return actualAmount;
    }

    /// <summary>
    /// Adds chips to the player's stack (e.g., from winning a pot).
    /// </summary>
    public void AddChips(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Cannot add negative chips", nameof(amount));
        }
        ChipStack += amount;
    }

    /// <summary>
    /// Marks the player as folded.
    /// </summary>
    public void Fold()
    {
        HasFolded = true;
    }

    /// <summary>
    /// Resets the player's current bet for a new betting round.
    /// </summary>
    public void ResetCurrentBet()
    {
        CurrentBet = 0;
    }

    /// <summary>
    /// Resets the player for a new hand.
    /// </summary>
    public void ResetForNewHand()
    {
        CurrentBet = 0;
        HasFolded = false;
    }

    /// <summary>
    /// Amount needed to call the current bet.
    /// </summary>
    public int AmountToCall(int currentBet)
    {
        return Math.Max(0, currentBet - CurrentBet);
    }

    /// <summary>
    /// Returns the maximum amount the player can bet.
    /// </summary>
    public int MaxBet => ChipStack;

    public override string ToString()
    {
        var status = HasFolded ? " (folded)" : IsAllIn ? " (all-in)" : "";
        return $"{Name}: {ChipStack} chips{status}";
    }
}
