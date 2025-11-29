using System.Collections.Generic;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Dealing;

/// <summary>
/// Represents the result of a card deal operation.
/// </summary>
public record DealResult(
    Card Card,
    string Recipient,
    DealCardType CardType,
    int DealSequence);

/// <summary>
/// Specifies the type of card being dealt.
/// </summary>
public enum DealCardType
{
    /// <summary>A hole card dealt face down to a player.</summary>
    HoleCard,
    
    /// <summary>A face-up card dealt to a player (stud games).</summary>
    FaceUpCard,
    
    /// <summary>A community card visible to all players.</summary>
    CommunityCard,
    
    /// <summary>A burn card discarded before dealing community cards.</summary>
    BurnCard
}

/// <summary>
/// Interface for variant-aware card dealing engines.
/// Handles dealing according to variant-specific rules (order, burn, community/hole distribution, stud patterns).
/// </summary>
public interface IDealingEngine
{
    /// <summary>
    /// Gets the variant identifier for this dealing engine.
    /// </summary>
    string VariantId { get; }

    /// <summary>
    /// Gets the current RNG seed used for dealing.
    /// Used for deterministic replay mode.
    /// </summary>
    int Seed { get; }

    /// <summary>
    /// Initializes the dealing engine for a new hand.
    /// </summary>
    /// <param name="playerNames">The names of players at the table in seat order.</param>
    /// <param name="dealerPosition">The position of the dealer button.</param>
    /// <param name="seed">Optional seed for deterministic dealing. If null, generates a random seed.</param>
    void Initialize(IReadOnlyList<string> playerNames, int dealerPosition, int? seed = null);

    /// <summary>
    /// Shuffles the deck and prepares for dealing.
    /// </summary>
    void Shuffle();

    /// <summary>
    /// Deals hole cards to all players according to variant rules.
    /// Returns the sequence of deal operations for animation.
    /// </summary>
    IReadOnlyList<DealResult> DealHoleCards();

    /// <summary>
    /// Deals community cards for the specified street (e.g., flop, turn, river).
    /// Returns the sequence of deal operations including any burn cards.
    /// </summary>
    /// <param name="streetName">The name of the street (e.g., "Flop", "Turn", "River").</param>
    IReadOnlyList<DealResult> DealCommunityCards(string streetName);

    /// <summary>
    /// Deals a single card to a specific player (for stud games).
    /// </summary>
    /// <param name="playerName">The name of the player to deal to.</param>
    /// <param name="faceUp">Whether the card is dealt face up.</param>
    DealResult DealToPlayer(string playerName, bool faceUp);

    /// <summary>
    /// Gets the number of hole cards dealt per player for this variant.
    /// </summary>
    int HoleCardsPerPlayer { get; }

    /// <summary>
    /// Gets whether this variant uses community cards.
    /// </summary>
    bool UsesCommunityCards { get; }

    /// <summary>
    /// Gets whether this variant uses burn cards before community cards.
    /// </summary>
    bool UsesBurnCards { get; }

    /// <summary>
    /// Gets the number of cards remaining in the deck.
    /// </summary>
    int CardsRemaining { get; }
}
