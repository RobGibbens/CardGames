using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using Xunit;
using System.Linq;

namespace CardGames.Poker.Tests.Evaluation;

public class BaseballHandDescriptionTests
{
    [Fact]
    public void BaseballHand_StraightFlush_With_Wilds_Description_Is_Correct()
    {
        // 3d (wild), 6d, 3h (wild), 9c (wild), 4s, 2h, 5s
        // Evaluates to Straight Flush to the 8 (4s, 5s + 3 wilds -> 4s, 5s, 6s, 7s, 8s)
        var allCards = "3d 6d 3h 9c 4s 2h 5s".ToCards();
        var hole = allCards.Take(2).ToList();
        var open = allCards.Skip(2).Take(4).ToList();
        var down = allCards.Skip(6).ToList();

        var hand = new BaseballHand(hole, open, down);

        var description = HandDescriptionFormatter.GetHandDescription(hand);

        description.Should().Be("Straight flush to the Eight");
    }
}
