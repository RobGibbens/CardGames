namespace CardGames.Poker.Betting;

/// <summary>
/// Represents a betting limit strategy that determines valid bet/raise amounts.
/// Different poker variants use different limit types (No Limit, Pot Limit, Fixed Limit).
/// </summary>
public interface ILimitStrategy
{
    /// <summary>
    /// Gets the minimum bet amount.
    /// </summary>
    /// <param name="bigBlind">The big blind amount.</param>
    /// <param name="currentBet">The current bet amount on the table.</param>
    /// <param name="lastRaiseAmount">The last raise amount.</param>
    /// <returns>The minimum bet amount.</returns>
    int GetMinBet(int bigBlind, int currentBet, int lastRaiseAmount);

    /// <summary>
    /// Gets the maximum bet amount.
    /// </summary>
    /// <param name="playerStack">The player's current chip stack.</param>
    /// <param name="currentPot">The current pot amount.</param>
    /// <param name="currentBet">The current bet amount on the table.</param>
    /// <param name="playerCurrentBet">The player's current bet in this round.</param>
    /// <returns>The maximum bet amount.</returns>
    int GetMaxBet(int playerStack, int currentPot, int currentBet, int playerCurrentBet);

    /// <summary>
    /// Gets the minimum raise amount.
    /// </summary>
    /// <param name="currentBet">The current bet amount on the table.</param>
    /// <param name="lastRaiseAmount">The last raise amount.</param>
    /// <param name="bigBlind">The big blind amount.</param>
    /// <returns>The minimum raise amount (total bet after raise).</returns>
    int GetMinRaise(int currentBet, int lastRaiseAmount, int bigBlind);

    /// <summary>
    /// Gets the maximum raise amount.
    /// </summary>
    /// <param name="playerStack">The player's current chip stack.</param>
    /// <param name="currentPot">The current pot amount.</param>
    /// <param name="currentBet">The current bet amount on the table.</param>
    /// <param name="playerCurrentBet">The player's current bet in this round.</param>
    /// <returns>The maximum raise amount (total bet after raise).</returns>
    int GetMaxRaise(int playerStack, int currentPot, int currentBet, int playerCurrentBet);

    /// <summary>
    /// Validates whether a bet amount is valid.
    /// </summary>
    /// <param name="amount">The bet amount to validate.</param>
    /// <param name="bigBlind">The big blind amount.</param>
    /// <param name="currentBet">The current bet amount on the table.</param>
    /// <param name="lastRaiseAmount">The last raise amount.</param>
    /// <param name="playerStack">The player's current chip stack.</param>
    /// <param name="currentPot">The current pot amount.</param>
    /// <param name="playerCurrentBet">The player's current bet in this round.</param>
    /// <returns>True if the bet amount is valid, false otherwise.</returns>
    bool IsValidBet(int amount, int bigBlind, int currentBet, int lastRaiseAmount, int playerStack, int currentPot, int playerCurrentBet);

    /// <summary>
    /// Validates whether a raise amount is valid.
    /// </summary>
    /// <param name="totalAmount">The total bet amount after the raise.</param>
    /// <param name="currentBet">The current bet amount on the table.</param>
    /// <param name="lastRaiseAmount">The last raise amount.</param>
    /// <param name="bigBlind">The big blind amount.</param>
    /// <param name="playerStack">The player's current chip stack.</param>
    /// <param name="currentPot">The current pot amount.</param>
    /// <param name="playerCurrentBet">The player's current bet in this round.</param>
    /// <returns>True if the raise amount is valid, false otherwise.</returns>
    bool IsValidRaise(int totalAmount, int currentBet, int lastRaiseAmount, int bigBlind, int playerStack, int currentPot, int playerCurrentBet);
}
