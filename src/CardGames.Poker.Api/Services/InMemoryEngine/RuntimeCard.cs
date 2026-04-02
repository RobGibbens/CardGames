using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Detached in-memory representation of a card in a game.
/// Mirrors <see cref="GameCard"/> without EF tracking.
/// </summary>
public sealed class RuntimeCard
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid? GamePlayerId { get; set; }
    public int HandNumber { get; set; }
    public CardSuit Suit { get; set; }
    public CardSymbol Symbol { get; set; }
    public CardLocation Location { get; set; }
    public int DealOrder { get; set; }
    public string? DealtAtPhase { get; set; }
    public bool IsVisible { get; set; }
    public bool IsWild { get; set; }
    public bool IsDiscarded { get; set; }
    public int? DiscardedAtDrawRound { get; set; }
    public bool IsDrawnCard { get; set; }
    public int? DrawnAtRound { get; set; }
    public bool IsBuyCard { get; set; }
    public DateTimeOffset DealtAt { get; set; }
}
