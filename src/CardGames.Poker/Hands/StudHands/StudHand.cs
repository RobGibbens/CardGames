using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.Strength;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Core.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.StudHands;

public class StudHand : HandBase
{
    protected override HandTypeStrengthRanking Ranking => HandTypeStrengthRanking.Classic;

    public IReadOnlyCollection<Card> HoleCards { get; }
    public IReadOnlyCollection<Card> OpenCards { get; }
    public IReadOnlyCollection<Card> DownCards { get; }

    public StudHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        IReadOnlyCollection<Card> downCards)
        : base(holeCards.Concat(openCards).Concat(downCards).ToList())
    {
        HoleCards = holeCards;
        OpenCards = openCards;
        DownCards = downCards;
    }

    protected override IEnumerable<IReadOnlyCollection<Card>> PossibleHands()
    {
        if (Cards.Count < 5)
        {
            return new[] { Cards };
        }

        return Cards
            .SubsetsOfSize(5)
            .Select(cards => cards.ToList());
    }

    /// <summary>
    /// Gets the best 5-card combination from the 7 available cards.
    /// </summary>
    /// <returns>The list of cards that make up the best hand.</returns>
    public IReadOnlyList<Card> GetBestHand()
    {
        var possibleHands = PossibleHands()
            .Select(hand => new
            {
                Cards = hand.ToList(),
                Type = HandTypeDetermination.DetermineHandType(hand)
            })
            .ToList();

        var bestType = HandStrength.GetEffectiveType(possibleHands.Select(h => h.Type), Ranking);

        return possibleHands
            .Where(h => h.Type == bestType)
            .OrderByDescending(h => HandStrength.Calculate(h.Cards, h.Type, Ranking))
            .First()
            .Cards;
    }
}
