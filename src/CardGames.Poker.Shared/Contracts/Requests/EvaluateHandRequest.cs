namespace CardGames.Poker.Shared.Contracts.Requests;

/// <summary>
/// Request to evaluate a poker hand.
/// </summary>
public record EvaluateHandRequest(IReadOnlyList<string> Cards);
