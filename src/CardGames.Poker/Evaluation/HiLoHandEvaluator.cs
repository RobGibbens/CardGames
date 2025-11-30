using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.Strength;
using System.Collections.Generic;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Evaluator for hi/lo split games (e.g., Omaha Hi/Lo, Stud Hi/Lo).
/// The pot is split between the best high hand and the best qualifying low hand.
/// </summary>
public sealed class HiLoHandEvaluator
{
    private readonly HighHandEvaluator _highEvaluator;
    private readonly LowHandEvaluator _lowEvaluator;

    public HiLoHandEvaluator(
        HandTypeStrengthRanking ranking = HandTypeStrengthRanking.Classic,
        bool requiresEightOrBetter = true)
    {
        _highEvaluator = new HighHandEvaluator(ranking);
        _lowEvaluator = new LowHandEvaluator(requiresEightOrBetter);
    }

    public static HiLoHandEvaluator Classic => new(HandTypeStrengthRanking.Classic, true);

    public static HiLoHandEvaluator ShortDeck => new(HandTypeStrengthRanking.ShortDeck, true);

    /// <summary>
    /// Evaluates both the high and low hands from the given cards.
    /// </summary>
    /// <param name="cards">The cards to evaluate.</param>
    /// <returns>Combined high and low evaluation results.</returns>
    public HiLoEvaluationResult Evaluate(IReadOnlyCollection<Card> cards)
    {
        var highResult = _highEvaluator.Evaluate(cards);
        var lowResult = _lowEvaluator.Evaluate(cards);

        return new HiLoEvaluationResult(highResult, lowResult);
    }

    /// <summary>
    /// Evaluates both the high and low hands from the given cards with wild card support.
    /// </summary>
    /// <param name="cards">The cards to evaluate.</param>
    /// <param name="wildCards">The wild cards that can substitute for any card.</param>
    /// <returns>Combined high and low evaluation results.</returns>
    public HiLoEvaluationResult Evaluate(IReadOnlyCollection<Card> cards, IReadOnlyCollection<Card> wildCards)
    {
        var highResult = _highEvaluator.Evaluate(cards, wildCards);
        var lowResult = _lowEvaluator.Evaluate(cards, wildCards);

        return new HiLoEvaluationResult(highResult, lowResult);
    }

    /// <summary>
    /// Compares two hi/lo hands and returns the split results.
    /// </summary>
    /// <param name="hand1">First hand's hi/lo result.</param>
    /// <param name="hand2">Second hand's hi/lo result.</param>
    /// <returns>Comparison result indicating who wins high, low, or both.</returns>
    public HiLoComparisonResult Compare(HiLoEvaluationResult hand1, HiLoEvaluationResult hand2)
    {
        var highComparison = _highEvaluator.Compare(hand1.High, hand2.High);
        var lowComparison = _lowEvaluator.Compare(hand1.Low, hand2.Low);

        return new HiLoComparisonResult(highComparison, lowComparison);
    }
}

/// <summary>
/// Combined evaluation result for hi/lo games.
/// </summary>
public sealed class HiLoEvaluationResult
{
    /// <summary>
    /// The high hand evaluation result.
    /// </summary>
    public HandEvaluationResult High { get; }

    /// <summary>
    /// The low hand evaluation result.
    /// If no qualifying low, Strength will be 0 and Type will be Incomplete.
    /// </summary>
    public HandEvaluationResult Low { get; }

    /// <summary>
    /// Whether this hand qualifies for low.
    /// </summary>
    public bool HasQualifyingLow => Low.Strength > 0;

    public HiLoEvaluationResult(HandEvaluationResult high, HandEvaluationResult low)
    {
        High = high;
        Low = low;
    }
}

/// <summary>
/// Result of comparing two hi/lo hands.
/// </summary>
public sealed class HiLoComparisonResult
{
    /// <summary>
    /// High hand comparison: positive if hand1 wins high, negative if hand2 wins, 0 for tie.
    /// </summary>
    public int HighComparison { get; }

    /// <summary>
    /// Low hand comparison: positive if hand1 wins low, negative if hand2 wins, 0 for tie or no qualifying low.
    /// </summary>
    public int LowComparison { get; }

    /// <summary>
    /// Whether hand1 wins the high pot.
    /// </summary>
    public bool Hand1WinsHigh => HighComparison > 0;

    /// <summary>
    /// Whether hand2 wins the high pot.
    /// </summary>
    public bool Hand2WinsHigh => HighComparison < 0;

    /// <summary>
    /// Whether the high pot is split (tie).
    /// </summary>
    public bool HighIsTie => HighComparison == 0;

    /// <summary>
    /// Whether hand1 wins the low pot.
    /// </summary>
    public bool Hand1WinsLow => LowComparison > 0;

    /// <summary>
    /// Whether hand2 wins the low pot.
    /// </summary>
    public bool Hand2WinsLow => LowComparison < 0;

    /// <summary>
    /// Whether the low pot is split (tie or no qualifying low).
    /// </summary>
    public bool LowIsTie => LowComparison == 0;

    /// <summary>
    /// Whether hand1 scoops (wins both high and low).
    /// </summary>
    public bool Hand1Scoops => Hand1WinsHigh && Hand1WinsLow;

    /// <summary>
    /// Whether hand2 scoops (wins both high and low).
    /// </summary>
    public bool Hand2Scoops => Hand2WinsHigh && Hand2WinsLow;

    public HiLoComparisonResult(int highComparison, int lowComparison)
    {
        HighComparison = highComparison;
        LowComparison = lowComparison;
    }
}
