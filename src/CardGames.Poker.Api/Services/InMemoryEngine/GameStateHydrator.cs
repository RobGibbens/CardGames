using CardGames.Poker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Loads game state from the database and maps EF entities to detached runtime models.
/// Uses <see cref="IServiceScopeFactory"/> to create a short-lived scope so that the
/// <see cref="CardsDbContext"/> is not held open beyond the hydration query.
/// </summary>
public sealed class GameStateHydrator : IGameStateHydrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameStateHydrator> _logger;

    public GameStateHydrator(IServiceScopeFactory scopeFactory, ILogger<GameStateHydrator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ActiveGameRuntimeState?> HydrateFromDatabaseAsync(
        Guid gameId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CardsDbContext>();

        var game = await db.Games
            .AsNoTracking()
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .Include(g => g.Pots)
                .ThenInclude(p => p.Contributions)
            .Include(g => g.BettingRounds)
                .ThenInclude(br => br.Actions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);

        if (game is null)
        {
            _logger.LogWarning("Game {GameId} not found during hydration", gameId);
            return null;
        }

        var state = new ActiveGameRuntimeState
        {
            // ── Identity ──
            GameId = game.Id,
            GameTypeId = game.GameTypeId,
            GameTypeCode = game.GameType?.Code ?? string.Empty,
            Name = game.Name,

            // ── Phase & Hand Tracking ──
            CurrentPhase = game.CurrentPhase,
            CurrentHandNumber = game.CurrentHandNumber,
            Status = game.Status,

            // ── Positions ──
            DealerPosition = game.DealerPosition,
            CurrentPlayerIndex = game.CurrentPlayerIndex,
            BringInPlayerIndex = game.BringInPlayerIndex,
            CurrentDrawPlayerIndex = game.CurrentDrawPlayerIndex,

            // ── Betting Structure ──
            Ante = game.Ante,
            SmallBlind = game.SmallBlind,
            BigBlind = game.BigBlind,
            BringIn = game.BringIn,
            SmallBet = game.SmallBet,
            BigBet = game.BigBet,
            MinBet = game.MinBet,
            MaxBuyIn = game.MaxBuyIn,

            // ── Configuration ──
            RequiresJoinApproval = game.RequiresJoinApproval,
            GameSettings = game.GameSettings,
            IsDealersChoice = game.IsDealersChoice,
            AreOddsVisibleToAllPlayers = game.AreOddsVisibleToAllPlayers,
            CurrentHandGameTypeCode = game.CurrentHandGameTypeCode,
            DealersChoiceDealerPosition = game.DealersChoiceDealerPosition,
            OriginalDealersChoiceDealerPosition = game.OriginalDealersChoiceDealerPosition,

            // ── Chip Check Pause ──
            IsPausedForChipCheck = game.IsPausedForChipCheck,
            ChipCheckPauseStartedAt = game.ChipCheckPauseStartedAt,
            ChipCheckPauseEndsAt = game.ChipCheckPauseEndsAt,

            // ── Timestamps ──
            CreatedAt = game.CreatedAt,
            UpdatedAt = game.UpdatedAt,
            StartedAt = game.StartedAt,
            EndedAt = game.EndedAt,
            HandCompletedAt = game.HandCompletedAt,
            NextHandStartsAt = game.NextHandStartsAt,
            DrawCompletedAt = game.DrawCompletedAt,

            // ── Audit ──
            RandomSeed = game.RandomSeed,
            CreatedById = game.CreatedById,
            CreatedByName = game.CreatedByName,
            UpdatedById = game.UpdatedById,
            UpdatedByName = game.UpdatedByName,

            // ── Versioning ──
            Version = 0,
            LastCheckpointRowVersion = game.RowVersion ?? [],
            IsDirty = false,

            // ── Collections ──
            Players = game.GamePlayers.Select(MapPlayer).ToList(),
            Cards = game.GameCards.Select(MapCard).ToList(),
            Pots = game.Pots.Select(MapPot).ToList(),
            BettingRounds = game.BettingRounds.Select(MapBettingRound).ToList(),
        };

        _logger.LogDebug(
            "Hydrated game {GameId} — {PlayerCount} players, {CardCount} cards, {PotCount} pots, {RoundCount} betting rounds",
            gameId, state.Players.Count, state.Cards.Count, state.Pots.Count, state.BettingRounds.Count);

        return state;
    }

    private static RuntimeGamePlayer MapPlayer(Data.Entities.GamePlayer gp) => new()
    {
        Id = gp.Id,
        GameId = gp.GameId,
        PlayerId = gp.PlayerId,
        PlayerName = gp.Player?.Name ?? string.Empty,
        PlayerEmail = gp.Player?.Email,
        ExternalId = gp.Player?.ExternalId,
        AvatarUrl = gp.Player?.AvatarUrl,
        SeatPosition = gp.SeatPosition,
        ChipStack = gp.ChipStack,
        StartingChips = gp.StartingChips,
        CurrentBet = gp.CurrentBet,
        TotalContributedThisHand = gp.TotalContributedThisHand,
        HasFolded = gp.HasFolded,
        IsAllIn = gp.IsAllIn,
        IsConnected = gp.IsConnected,
        IsSittingOut = gp.IsSittingOut,
        DropOrStayDecision = gp.DropOrStayDecision,
        AutoDropOnDropOrStay = gp.AutoDropOnDropOrStay,
        HasDrawnThisRound = gp.HasDrawnThisRound,
        JoinedAtHandNumber = gp.JoinedAtHandNumber,
        LeftAtHandNumber = gp.LeftAtHandNumber,
        FinalChipCount = gp.FinalChipCount,
        PendingChipsToAdd = gp.PendingChipsToAdd,
        BringInAmount = gp.BringInAmount,
        Status = gp.Status,
        VariantState = gp.VariantState,
        JoinedAt = gp.JoinedAt,
        LeftAt = gp.LeftAt,
        LastCheckpointRowVersion = gp.RowVersion ?? [],
    };

    private static RuntimeCard MapCard(Data.Entities.GameCard gc) => new()
    {
        Id = gc.Id,
        GameId = gc.GameId,
        GamePlayerId = gc.GamePlayerId,
        HandNumber = gc.HandNumber,
        Suit = gc.Suit,
        Symbol = gc.Symbol,
        Location = gc.Location,
        DealOrder = gc.DealOrder,
        DealtAtPhase = gc.DealtAtPhase,
        IsVisible = gc.IsVisible,
        IsWild = gc.IsWild,
        IsDiscarded = gc.IsDiscarded,
        DiscardedAtDrawRound = gc.DiscardedAtDrawRound,
        IsDrawnCard = gc.IsDrawnCard,
        DrawnAtRound = gc.DrawnAtRound,
        IsBuyCard = gc.IsBuyCard,
        DealtAt = gc.DealtAt,
    };

    private static RuntimePot MapPot(Data.Entities.Pot p) => new()
    {
        Id = p.Id,
        GameId = p.GameId,
        HandNumber = p.HandNumber,
        PotType = p.PotType,
        PotOrder = p.PotOrder,
        Amount = p.Amount,
        MaxContributionPerPlayer = p.MaxContributionPerPlayer,
        IsAwarded = p.IsAwarded,
        AwardedAt = p.AwardedAt,
        WinnerPayouts = p.WinnerPayouts,
        WinReason = p.WinReason,
        CreatedAt = p.CreatedAt,
        Contributions = p.Contributions.Select(MapPotContribution).ToList(),
    };

    private static RuntimePotContribution MapPotContribution(Data.Entities.PotContribution pc) => new()
    {
        Id = pc.Id,
        PotId = pc.PotId,
        GamePlayerId = pc.GamePlayerId,
        Amount = pc.Amount,
        IsEligibleToWin = pc.IsEligibleToWin,
        IsPotMatch = pc.IsPotMatch,
        ContributedAt = pc.ContributedAt,
    };

    private static RuntimeBettingRound MapBettingRound(Data.Entities.BettingRound br) => new()
    {
        Id = br.Id,
        GameId = br.GameId,
        HandNumber = br.HandNumber,
        RoundNumber = br.RoundNumber,
        Street = br.Street,
        CurrentBet = br.CurrentBet,
        MinBet = br.MinBet,
        RaiseCount = br.RaiseCount,
        MaxRaises = br.MaxRaises,
        LastRaiseAmount = br.LastRaiseAmount,
        PlayersInHand = br.PlayersInHand,
        PlayersActed = br.PlayersActed,
        CurrentActorIndex = br.CurrentActorIndex,
        LastAggressorIndex = br.LastAggressorIndex,
        IsComplete = br.IsComplete,
        StartedAt = br.StartedAt,
        CompletedAt = br.CompletedAt,
        Actions = br.Actions.Select(MapBettingAction).ToList(),
    };

    private static RuntimeBettingAction MapBettingAction(Data.Entities.BettingActionRecord ba) => new()
    {
        Id = ba.Id,
        BettingRoundId = ba.BettingRoundId,
        GamePlayerId = ba.GamePlayerId,
        ActionOrder = ba.ActionOrder,
        ActionType = ba.ActionType,
        Amount = ba.Amount,
        ChipsMoved = ba.ChipsMoved,
        ChipStackBefore = ba.ChipStackBefore,
        ChipStackAfter = ba.ChipStackAfter,
        PotBefore = ba.PotBefore,
        PotAfter = ba.PotAfter,
        DecisionTimeSeconds = ba.DecisionTimeSeconds,
        IsForced = ba.IsForced,
        IsTimeout = ba.IsTimeout,
        Note = ba.Note,
        ActionAt = ba.ActionAt,
    };
}
