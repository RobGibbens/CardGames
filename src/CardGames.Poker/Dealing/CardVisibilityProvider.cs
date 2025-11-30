using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Dealing;

/// <summary>
/// Card visibility provider for community card games (Hold'em, Omaha).
/// Hole cards are private; community cards are visible to all.
/// </summary>
public class CommunityCardVisibilityProvider : ICardVisibilityProvider
{
    private readonly string _variantId;

    /// <summary>
    /// Creates a visibility provider for a community card game.
    /// </summary>
    /// <param name="variantId">The variant identifier (e.g., "holdem", "omaha").</param>
    public CommunityCardVisibilityProvider(string variantId)
    {
        _variantId = variantId;
    }

    /// <inheritdoc/>
    public string VariantId => _variantId;

    /// <inheritdoc/>
    public IReadOnlyList<CardVisibility> GetVisibility(DealCardType cardType, string recipient, IReadOnlyList<string> allPlayers)
    {
        return allPlayers
            .Select(player => new CardVisibility(
                PlayerName: player,
                IsFaceUp: IsFaceUp(cardType),
                CanSeeCard: CanViewCard(cardType, recipient, player)))
            .ToList();
    }

    /// <inheritdoc/>
    public bool CanViewCard(DealCardType cardType, string recipient, string viewer)
    {
        return cardType switch
        {
            // Hole cards: only the owner can see them
            DealCardType.HoleCard => recipient == viewer,
            
            // Community cards: everyone can see them
            DealCardType.CommunityCard => true,
            
            // Burn cards: traditionally hidden, but some games allow viewing at end
            DealCardType.BurnCard => false,
            
            // Face-up cards don't apply to community card games
            DealCardType.FaceUpCard => true,
            
            _ => false
        };
    }

    /// <inheritdoc/>
    public bool IsFaceUp(DealCardType cardType)
    {
        return cardType switch
        {
            DealCardType.HoleCard => false,
            DealCardType.CommunityCard => true,
            DealCardType.FaceUpCard => true,
            DealCardType.BurnCard => false,
            _ => false
        };
    }
}

/// <summary>
/// Card visibility provider for stud poker games.
/// Supports mixed visibility with some cards face-up and others face-down.
/// </summary>
public class StudVisibilityProvider : ICardVisibilityProvider
{
    private readonly string _variantId;

    /// <summary>
    /// Creates a visibility provider for a stud game.
    /// </summary>
    /// <param name="variantId">The variant identifier (e.g., "seven-card-stud").</param>
    public StudVisibilityProvider(string variantId)
    {
        _variantId = variantId;
    }

    /// <inheritdoc/>
    public string VariantId => _variantId;

    /// <inheritdoc/>
    public IReadOnlyList<CardVisibility> GetVisibility(DealCardType cardType, string recipient, IReadOnlyList<string> allPlayers)
    {
        return allPlayers
            .Select(player => new CardVisibility(
                PlayerName: player,
                IsFaceUp: IsFaceUp(cardType),
                CanSeeCard: CanViewCard(cardType, recipient, player)))
            .ToList();
    }

    /// <inheritdoc/>
    public bool CanViewCard(DealCardType cardType, string recipient, string viewer)
    {
        return cardType switch
        {
            // Hole cards (face down): only the owner can see them
            DealCardType.HoleCard => recipient == viewer,
            
            // Face-up cards: everyone can see them
            DealCardType.FaceUpCard => true,
            
            // Community cards: everyone can see them (rare in stud, but supported)
            DealCardType.CommunityCard => true,
            
            // Burn cards: hidden
            DealCardType.BurnCard => false,
            
            _ => false
        };
    }

    /// <inheritdoc/>
    public bool IsFaceUp(DealCardType cardType)
    {
        return cardType switch
        {
            DealCardType.HoleCard => false,
            DealCardType.FaceUpCard => true,
            DealCardType.CommunityCard => true,
            DealCardType.BurnCard => false,
            _ => false
        };
    }
}

/// <summary>
/// Factory for creating card visibility providers based on variant.
/// </summary>
public static class CardVisibilityProviderFactory
{
    /// <summary>
    /// Creates a card visibility provider for the specified variant.
    /// </summary>
    /// <param name="variantId">The variant identifier.</param>
    /// <returns>An appropriate visibility provider for the variant.</returns>
    public static ICardVisibilityProvider Create(string variantId)
    {
        return variantId.ToLowerInvariant() switch
        {
            "holdem" or "texas-holdem" => new CommunityCardVisibilityProvider(variantId),
            "omaha" or "omaha-hi" or "omaha-hilo" => new CommunityCardVisibilityProvider(variantId),
            "seven-card-stud" or "stud" or "stud-hi" or "stud-hilo" => new StudVisibilityProvider(variantId),
            "razz" => new StudVisibilityProvider(variantId),
            _ => new CommunityCardVisibilityProvider(variantId)
        };
    }
}
