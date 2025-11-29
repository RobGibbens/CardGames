using System;

namespace CardGames.Poker.Betting;

/// <summary>
/// Fixed Limit betting strategy where bets and raises are fixed amounts.
/// Typically uses small bet for early rounds and big bet for later rounds.
/// </summary>
public class FixedLimitStrategy : ILimitStrategy
{
    private readonly int _smallBet;
    private readonly int _bigBet;
    private readonly bool _useBigBet;

    /// <summary>
    /// Creates a fixed limit strategy for early betting rounds (uses small bet).
    /// </summary>
    /// <param name="smallBet">The small bet amount (typically equal to big blind).</param>
    /// <param name="bigBet">The big bet amount (typically 2x small bet).</param>
    public FixedLimitStrategy(int smallBet, int bigBet)
        : this(smallBet, bigBet, false)
    {
    }

    /// <summary>
    /// Creates a fixed limit strategy.
    /// </summary>
    /// <param name="smallBet">The small bet amount (typically equal to big blind).</param>
    /// <param name="bigBet">The big bet amount (typically 2x small bet).</param>
    /// <param name="useBigBet">True for later betting rounds (turn/river), false for early rounds (preflop/flop).</param>
    public FixedLimitStrategy(int smallBet, int bigBet, bool useBigBet)
    {
        _smallBet = smallBet;
        _bigBet = bigBet;
        _useBigBet = useBigBet;
    }

    /// <summary>
    /// Gets the current betting increment (small bet or big bet based on round).
    /// </summary>
    public int BettingIncrement => _useBigBet ? _bigBet : _smallBet;

    public int GetMinBet(int bigBlind, int currentBet, int lastRaiseAmount)
    {
        return BettingIncrement;
    }

    public int GetMaxBet(int playerStack, int currentPot, int currentBet, int playerCurrentBet)
    {
        // In fixed limit, max bet is always the fixed increment
        return Math.Min(BettingIncrement, playerStack);
    }

    public int GetMinRaise(int currentBet, int lastRaiseAmount, int bigBlind)
    {
        // In fixed limit, raise is always the fixed increment
        return currentBet + BettingIncrement;
    }

    public int GetMaxRaise(int playerStack, int currentPot, int currentBet, int playerCurrentBet)
    {
        // In fixed limit, raise is always the fixed increment
        var amountToContribute = (currentBet + BettingIncrement) - playerCurrentBet;
        if (amountToContribute > playerStack)
        {
            // Player can only go all-in
            return playerCurrentBet + playerStack;
        }
        return currentBet + BettingIncrement;
    }

    public bool IsValidBet(int amount, int bigBlind, int currentBet, int lastRaiseAmount, int playerStack, int currentPot, int playerCurrentBet)
    {
        // Cannot bet if there's already a bet
        if (currentBet > 0)
        {
            return false;
        }

        // Bet must be exactly the fixed amount, or all-in if less
        if (amount != BettingIncrement && amount != playerStack)
        {
            return false;
        }

        // Cannot bet more than your stack
        if (amount > playerStack)
        {
            return false;
        }

        return true;
    }

    public bool IsValidRaise(int totalAmount, int currentBet, int lastRaiseAmount, int bigBlind, int playerStack, int currentPot, int playerCurrentBet)
    {
        // Cannot raise if there's no bet
        if (currentBet == 0)
        {
            return false;
        }

        var expectedRaise = currentBet + BettingIncrement;
        var amountToContribute = totalAmount - playerCurrentBet;

        // Cannot raise more than your stack
        if (amountToContribute > playerStack)
        {
            return false;
        }

        // Raise must be exactly the fixed increment, or all-in if player has less
        if (totalAmount != expectedRaise && amountToContribute != playerStack)
        {
            return false;
        }

        // Raise must be larger than the current bet
        if (totalAmount <= currentBet)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a new strategy for the next betting stage.
    /// </summary>
    /// <param name="useBigBet">Whether to use the big bet for the new stage.</param>
    /// <returns>A new FixedLimitStrategy with the specified bet size.</returns>
    public FixedLimitStrategy ForStage(bool useBigBet)
    {
        return new FixedLimitStrategy(_smallBet, _bigBet, useBigBet);
    }
}
