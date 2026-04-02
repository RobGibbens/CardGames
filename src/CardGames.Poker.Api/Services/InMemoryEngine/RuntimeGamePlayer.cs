using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Detached in-memory representation of a player's state within a game.
/// Mirrors <see cref="GamePlayer"/> without EF tracking.
/// </summary>
public sealed class RuntimeGamePlayer
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }

    /// <summary>Player's display name (from Player entity).</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>Player's email (from Player entity, used for SignalR user routing).</summary>
    public string? PlayerEmail { get; set; }

    /// <summary>Player's external identity provider ID (from Player entity).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Player's avatar URL (from Player entity).</summary>
    public string? AvatarUrl { get; set; }

    public int SeatPosition { get; set; }
    public int ChipStack { get; set; }
    public int StartingChips { get; set; }
    public int CurrentBet { get; set; }
    public int TotalContributedThisHand { get; set; }
    public bool HasFolded { get; set; }
    public bool IsAllIn { get; set; }
    public bool IsConnected { get; set; } = true;
    public bool IsSittingOut { get; set; }
    public DropOrStayDecision? DropOrStayDecision { get; set; }
    public bool AutoDropOnDropOrStay { get; set; }
    public bool HasDrawnThisRound { get; set; }
    public int JoinedAtHandNumber { get; set; } = 1;
    public int LeftAtHandNumber { get; set; } = -1;
    public int? FinalChipCount { get; set; }
    public int PendingChipsToAdd { get; set; }
    public int BringInAmount { get; set; }
    public GamePlayerStatus Status { get; set; } = GamePlayerStatus.Active;
    public string? VariantState { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>
    /// The SQL Server <c>RowVersion</c> from the last checkpoint.
    /// </summary>
    public byte[] LastCheckpointRowVersion { get; set; } = [];
}
