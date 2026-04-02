using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Detached in-memory representation of a pot. Mirrors <see cref="Pot"/> without EF tracking.
/// </summary>
public sealed class RuntimePot
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int HandNumber { get; set; }
    public PotType PotType { get; set; }
    public int PotOrder { get; set; }
    public int Amount { get; set; }
    public int? MaxContributionPerPlayer { get; set; }
    public bool IsAwarded { get; set; }
    public DateTimeOffset? AwardedAt { get; set; }
    public string? WinnerPayouts { get; set; }
    public string? WinReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<RuntimePotContribution> Contributions { get; set; } = [];
}

/// <summary>
/// Detached in-memory representation of a pot contribution.
/// Mirrors <see cref="PotContribution"/> without EF tracking.
/// </summary>
public sealed class RuntimePotContribution
{
    public Guid Id { get; set; }
    public Guid PotId { get; set; }
    public Guid GamePlayerId { get; set; }
    public int Amount { get; set; }
    public bool IsEligibleToWin { get; set; } = true;
    public bool IsPotMatch { get; set; }
    public DateTimeOffset ContributedAt { get; set; }
}
