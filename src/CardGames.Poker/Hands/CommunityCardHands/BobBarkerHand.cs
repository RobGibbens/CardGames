using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands.CommunityCardHands;

public sealed class BobBarkerHand : CommunityCardsHand
{
    private IReadOnlyCollection<Card>? _evaluatedBestCards;

    /// <summary>
    /// Gets the evaluated best 5-card hand honoring Omaha rules (exactly 2 hole + 3 community).
    /// </summary>
    public IReadOnlyCollection<Card> EvaluatedBestCards => _evaluatedBestCards ??= FindBestFiveCardHand();

    public BobBarkerHand(IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)
        : base(2, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
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