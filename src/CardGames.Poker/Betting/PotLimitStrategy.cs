using System;

namespace CardGames.Poker.Betting;

/// <summary>
/// Pot Limit betting strategy where players can bet up to the current pot size.
/// </summary>
public class PotLimitStrategy : ILimitStrategy
{
    public int GetMinBet(int bigBlind, int currentBet, int lastRaiseAmount)
    {
        return bigBlind;
    }

    public int GetMaxBet(int playerStack, int currentPot, int currentBet, int playerCurrentBet)
    {
        // In pot limit, max bet is the current pot size
        var maxPotBet = currentPot;
        return Math.Min(maxPotBet, playerStack);
    }

    public int GetMinRaise(int currentBet, int lastRaiseAmount, int bigBlind)
    {
        // Minimum raise must be at least the size of the previous raise
        var raiseSize = Math.Max(lastRaiseAmount, bigBlind);
        return currentBet + raiseSize;
    }

    public int GetMaxRaise(int playerStack, int currentPot, int currentBet, int playerCurrentBet)
    {
        // In pot limit, max raise is calculated as:
        // Current pot + amount to call + amount to call (the matching portion of the raise)
        // This equals: pot + 2 * (currentBet - playerCurrentBet) + currentBet - playerCurrentBet
        // Simplified: pot + 3 * callAmount where callAmount = currentBet - playerCurrentBet
        var amountToCall = currentBet - playerCurrentBet;
        var potAfterCall = currentPot + amountToCall;
        var maxRaiseAmount = potAfterCall;
        var totalMaxBet = currentBet + maxRaiseAmount;
        
        // Cannot exceed player's stack
        var maxFromStack = playerCurrentBet + playerStack;
        return Math.Min(totalMaxBet, maxFromStack);
    }

    public bool IsValidBet(int amount, int bigBlind, int currentBet, int lastRaiseAmount, int playerStack, int currentPot, int playerCurrentBet)
    {
        // Cannot bet if there's already a bet
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

        // Cannot bet more than the pot size (unless all-in for less)
        var maxBet = GetMaxBet(playerStack, currentPot, currentBet, playerCurrentBet);
        if (amount > maxBet && amount != playerStack)
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

        // Cannot raise more than pot limit
        var maxRaise = GetMaxRaise(playerStack, currentPot, currentBet, playerCurrentBet);
        if (totalAmount > maxRaise && amountToContribute != playerStack)
        {
            return false;
        }

        return true;
    }
}
