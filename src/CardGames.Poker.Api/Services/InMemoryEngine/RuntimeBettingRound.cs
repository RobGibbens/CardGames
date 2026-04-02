using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Detached in-memory representation of a betting round.
/// Mirrors <see cref="BettingRound"/> without EF tracking.
/// </summary>
public sealed class RuntimeBettingRound
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int HandNumber { get; set; }
    public int RoundNumber { get; set; }
    public string Street { get; set; } = string.Empty;
    public int CurrentBet { get; set; }
    public int MinBet { get; set; }
    public int RaiseCount { get; set; }
    public int MaxRaises { get; set; }
    public int LastRaiseAmount { get; set; }
    public int PlayersInHand { get; set; }
    public int PlayersActed { get; set; }
    public int CurrentActorIndex { get; set; }
    public int LastAggressorIndex { get; set; } = -1;
    public bool IsComplete { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<RuntimeBettingAction> Actions { get; set; } = [];
}

/// <summary>
/// Detached in-memory representation of a betting action.
/// Mirrors <see cref="BettingActionRecord"/> without EF tracking.
/// </summary>
public sealed class RuntimeBettingAction
{
    public Guid Id { get; set; }
    public Guid BettingRoundId { get; set; }
    public Guid GamePlayerId { get; set; }
    public int ActionOrder { get; set; }
    public BettingActionType ActionType { get; set; }
    public int Amount { get; set; }
    public int ChipsMoved { get; set; }
    public int ChipStackBefore { get; set; }
    public int ChipStackAfter { get; set; }
    public int PotBefore { get; set; }
    public int PotAfter { get; set; }
    public double? DecisionTimeSeconds { get; set; }
    public bool IsForced { get; set; }
    public bool IsTimeout { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset ActionAt { get; set; }
}
