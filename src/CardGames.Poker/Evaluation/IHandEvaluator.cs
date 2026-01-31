using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Interface for game-specific hand evaluation.
/// Each poker variant can have its own hand evaluator that knows how to
/// create and evaluate hands according to that variant's rules.
/// </summary>
public interface IHandEvaluator
{
    /// <summary>
    /// Creates a hand from the given cards using this evaluator's rules.
    /// </summary>
    /// <param name="cards">The cards to evaluate.</param>
    /// <returns>A <see cref="HandBase"/> representing the evaluated hand.</returns>
    HandBase CreateHand(IReadOnlyCollection<Card> cards);

    /// <summary>
    /// Creates a hand from cards with explicit hole and board card separation.
    /// Used for stud-style games where card positions matter.
    /// </summary>
    /// <param name="holeCards">Cards dealt face-down to the player.</param>
    /// <param name="boardCards">Cards dealt face-up on the board.</param>
    /// <param name="downCards">Additional face-down cards (e.g., seventh street in 7-card stud).</param>
    /// <returns>A <see cref="HandBase"/> representing the evaluated hand.</returns>
    HandBase CreateHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> boardCards,
        IReadOnlyCollection<Card> downCards);

    /// <summary>
    /// Gets the indexes of wild cards within the provided card collection.
    /// </summary>
    /// <param name="cards">The cards to check for wild cards.</param>
    /// <returns>A collection of zero-based indexes indicating which cards are wild.</returns>
    IReadOnlyCollection<int> GetWildCardIndexes(IReadOnlyCollection<Card> cards);

    /// <summary>
    /// Gets the best evaluated cards for display after wild card substitution.
    /// For hands without wild cards, returns the original cards.
    /// </summary>
    /// <param name="hand">The hand to get evaluated cards from.</param>
    /// <returns>The best cards after any wild card substitutions.</returns>
    IReadOnlyCollection<Card> GetEvaluatedBestCards(HandBase hand);

    /// <summary>
    /// Gets whether this evaluator supports positional cards (hole/board separation).
    /// </summary>
    bool SupportsPositionalCards { get; }

    /// <summary>
    /// Gets whether this evaluator handles wild cards.
    /// </summary>
    bool HasWildCards { get; }
}
