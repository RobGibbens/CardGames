using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Detached in-memory representation of a game's complete state during active play.
/// This is the source of truth for game mutations while the engine is active,
/// replacing direct <c>CardsDbContext</c> reads during gameplay.
/// </summary>
/// <remarks>
/// IMPORTANT: This class must NEVER hold EF-tracked entities or <c>DbContext</c>-attached graphs.
/// All properties are plain CLR objects safe for concurrent reads under the per-game lock.
/// Mutations are only permitted inside <see cref="IGameExecutionCoordinator.ExecuteAsync{T}"/>.
/// </remarks>
public sealed class ActiveGameRuntimeState
{
    // ── Identity ──

    public Guid GameId { get; set; }
    public Guid? GameTypeId { get; set; }
    public string GameTypeCode { get; set; } = string.Empty;
    public string? Name { get; set; }

    // ── Phase & Hand Tracking ──

    public string CurrentPhase { get; set; } = string.Empty;
    public int CurrentHandNumber { get; set; } = 1;
    public GameStatus Status { get; set; } = GameStatus.WaitingForPlayers;

    // ── Positions ──

    public int DealerPosition { get; set; }
    public int CurrentPlayerIndex { get; set; } = -1;
    public int BringInPlayerIndex { get; set; } = -1;
    public int CurrentDrawPlayerIndex { get; set; } = -1;

    // ── Betting Structure ──

    public int? Ante { get; set; }
    public int? SmallBlind { get; set; }
    public int? BigBlind { get; set; }
    public int? BringIn { get; set; }
    public int? SmallBet { get; set; }
    public int? BigBet { get; set; }
    public int? MinBet { get; set; }
    public int? MaxBuyIn { get; set; }

    // ── Configuration ──

    public bool RequiresJoinApproval { get; set; }
    public string? GameSettings { get; set; }
    public bool IsDealersChoice { get; set; }
    public bool AreOddsVisibleToAllPlayers { get; set; } = true;
    public string? CurrentHandGameTypeCode { get; set; }
    public int? DealersChoiceDealerPosition { get; set; }
    public int? OriginalDealersChoiceDealerPosition { get; set; }

    // ── Chip Check Pause ──

    public bool IsPausedForChipCheck { get; set; }
    public DateTimeOffset? ChipCheckPauseStartedAt { get; set; }
    public DateTimeOffset? ChipCheckPauseEndsAt { get; set; }

    // ── Timestamps ──

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public DateTimeOffset? HandCompletedAt { get; set; }
    public DateTimeOffset? NextHandStartsAt { get; set; }
    public DateTimeOffset? DrawCompletedAt { get; set; }

    // ── Audit ──

    public int? RandomSeed { get; set; }
    public string? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public string? UpdatedById { get; set; }
    public string? UpdatedByName { get; set; }

    // ── Versioning ──

    /// <summary>
    /// Monotonic version counter, incremented on every mutation.
    /// Replaces SQL Server <c>RowVersion</c> for in-memory ordering.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// The SQL Server <c>RowVersion</c> from the last checkpoint.
    /// Used to map back to DB state for checkpoint writes.
    /// </summary>
    public byte[] LastCheckpointRowVersion { get; set; } = [];

    /// <summary>
    /// Whether any mutations have occurred since the last checkpoint.
    /// </summary>
    public bool IsDirty { get; set; }

    // ── Collections ──

    public List<RuntimeGamePlayer> Players { get; set; } = [];
    public List<RuntimeCard> Cards { get; set; } = [];
    public List<RuntimePot> Pots { get; set; } = [];
    public List<RuntimeBettingRound> BettingRounds { get; set; } = [];
}
