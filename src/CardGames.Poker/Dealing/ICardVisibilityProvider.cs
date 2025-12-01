using System.Collections.Generic;

namespace CardGames.Poker.Dealing;

/// <summary>
/// Represents a player's view of dealt cards, respecting visibility rules.
/// </summary>
public record CardVisibility(
    string PlayerName,
    bool IsFaceUp,
    bool CanSeeCard);

/// <summary>
/// Provides card visibility rules for different poker variants.
/// Determines which players can see which cards based on the variant's rules.
/// </summary>
public interface ICardVisibilityProvider
{
    /// <summary>
    /// Gets the variant identifier for this visibility provider.
    /// </summary>
    string VariantId { get; }

    /// <summary>
    /// Determines who can see a dealt card based on the card type and recipient.
    /// </summary>
    /// <param name="cardType">The type of card being dealt.</param>
    /// <param name="recipient">The recipient of the card.</param>
    /// <param name="allPlayers">All players at the table.</param>
    /// <returns>Visibility information for each player.</returns>
    IReadOnlyList<CardVisibility> GetVisibility(DealCardType cardType, string recipient, IReadOnlyList<string> allPlayers);

    /// <summary>
    /// Determines if a specific viewer can see a card dealt to a recipient.
    /// </summary>
    /// <param name="cardType">The type of card being dealt.</param>
    /// <param name="recipient">The recipient of the card.</param>
    /// <param name="viewer">The player trying to view the card.</param>
    /// <returns>True if the viewer can see the card, false otherwise.</returns>
    bool CanViewCard(DealCardType cardType, string recipient, string viewer);

    /// <summary>
    /// Determines if a card type is dealt face up or face down by default.
    /// </summary>
    /// <param name="cardType">The type of card.</param>
    /// <returns>True if the card is dealt face up, false if face down.</returns>
    bool IsFaceUp(DealCardType cardType);
}
