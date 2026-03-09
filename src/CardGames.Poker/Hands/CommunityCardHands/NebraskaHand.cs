using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.CommunityCardHands;

/// <summary>
/// Nebraska hand evaluator.
/// Must use exactly 3 hole cards and 2 community cards.
/// </summary>
public sealed class NebraskaHand : CommunityCardsHand
{
    private IReadOnlyCollection<Card>? _evaluatedBestCards;

    /// <summary>
    /// Gets the evaluated best 5-card hand honoring Nebraska rules (exactly 3 hole + 2 community).
    /// </summary>
    public IReadOnlyCollection<Card> EvaluatedBestCards => _evaluatedBestCards ??= FindBestFiveCardHand();

    public NebraskaHand(IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)
        : base(3, 3, holeCards, communityCards, HandTypeStrengthRanking.Classic)
    {
    }

    private IReadOnlyCollection<Card> FindBestFiveCardHand()
    {
        return PossibleHands()
            .Select(hand => new { hand, type = HandTypeDetermination.DetermineHandType(hand) })
            .Where(pair => pair.type == Type)
            .OrderByDescending(pair => HandStrength.Calculate(pair.hand.ToList(), pair.type, Ranking))
            .First()
            .hand
            .ToList();
    }
}
