using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.HandTypes;
using System.Collections.Generic;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Adapter interface for hand evaluation, supporting different evaluation variants.
/// </summary>
public interface IHandEvaluator
{
    /// <summary>
    /// Evaluates the best possible hand from the given cards.
    /// </summary>
    /// <param name="cards">The cards to evaluate.</param>
    /// <returns>The evaluation result containing hand type, strength, and winning cards.</returns>
    HandEvaluationResult Evaluate(IReadOnlyCollection<Card> cards);

    /// <summary>
    /// Evaluates the best possible hand from the given cards with wild card support.
    /// </summary>
    /// <param name="cards">The cards to evaluate.</param>
    /// <param name="wildCards">The wild cards that can substitute for any card.</param>
    /// <returns>The evaluation result containing hand type, strength, and winning cards.</returns>
    HandEvaluationResult Evaluate(IReadOnlyCollection<Card> cards, IReadOnlyCollection<Card> wildCards);

    /// <summary>
    /// Compares two hands and returns the winner.
    /// </summary>
    /// <param name="hand1">First hand's evaluation result.</param>
    /// <param name="hand2">Second hand's evaluation result.</param>
    /// <returns>Positive if hand1 wins, negative if hand2 wins, 0 for tie.</returns>
    int Compare(HandEvaluationResult hand1, HandEvaluationResult hand2);
}
