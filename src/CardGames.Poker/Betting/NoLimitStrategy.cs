using System;

namespace CardGames.Poker.Betting;

/// <summary>
/// No Limit betting strategy where players can bet any amount up to their entire stack.
/// </summary>
public class NoLimitStrategy : ILimitStrategy
{
    public int GetMinBet(int bigBlind, int currentBet, int lastRaiseAmount)
    {
        return bigBlind;
    }

    public int GetMaxBet(int playerStack, int currentPot, int currentBet, int playerCurrentBet)
    {
        return playerStack;
    }

    public int GetMinRaise(int currentBet, int lastRaiseAmount, int bigBlind)
    {
        // Minimum raise must be at least the size of the previous raise
        // If no raise yet, minimum raise is the big blind
        var raiseSize = Math.Max(lastRaiseAmount, bigBlind);
        return currentBet + raiseSize;
    }

    public int GetMaxRaise(int playerStack, int currentPot, int currentBet, int playerCurrentBet)
    {
        // In no limit, max raise is player's entire stack
        return playerCurrentBet + playerStack;
    }

    public bool IsValidBet(int amount, int bigBlind, int currentBet, int lastRaiseAmount, int playerStack, int currentPot, int playerCurrentBet)
    {
        // Cannot bet if there's already a bet (must raise instead)
        if (currentBet > 0)
        {
            return false;
        }

        // Bet must be at least the big blind, unless going all-in
        if (amount < bigBlind && amount != playerStack)
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
        // Cannot raise if there's no bet to raise
        if (currentBet == 0)
        {
            return false;
        }

        var amountToContribute = totalAmount - playerCurrentBet;

        // Cannot raise more than your stack
        if (amountToContribute > playerStack)
        {
            return false;
        }

        // Calculate minimum raise
        var minRaise = GetMinRaise(currentBet, lastRaiseAmount, bigBlind);

        // Raise must meet minimum unless going all-in
        if (totalAmount < minRaise && amountToContribute != playerStack)
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
}
