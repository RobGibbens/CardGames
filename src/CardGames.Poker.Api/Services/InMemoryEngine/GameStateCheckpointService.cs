using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Writes the current in-memory game state back to the database.
/// Uses a fresh scoped <see cref="CardsDbContext"/> to avoid long-lived tracking contexts.
/// </summary>
/// <remarks>
/// Strategy: load the full entity graph with tracking, update all scalar properties
/// from the runtime state, then call <c>SaveChangesAsync</c>. EF Core's change tracker
/// handles INSERT/UPDATE/DELETE of child collections automatically.
/// </remarks>
public sealed class GameStateCheckpointService : IGameStateCheckpointService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameStateCheckpointService> _logger;

    public GameStateCheckpointService(
        IServiceScopeFactory scopeFactory,
        ILogger<GameStateCheckpointService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CheckpointAsync(ActiveGameRuntimeState state, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CardsDbContext>();

        // Load tracked entity graph
        var game = await db.Games
            .Include(g => g.GamePlayers)
            .Include(g => g.GameCards)
            .Include(g => g.Pots)
                .ThenInclude(p => p.Contributions)
            .Include(g => g.BettingRounds)
                .ThenInclude(br => br.Actions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == state.GameId, cancellationToken);

        if (game is null)
        {
            _logger.LogError("Checkpoint failed — game {GameId} not found in database", state.GameId);
            return;
        }

        // ── Update Game scalars ──
        ApplyGameState(game, state);

        // ── Sync Players ──
        SyncPlayers(db, game, state);

        // ── Sync Cards ──
        SyncCards(db, game, state);

        // ── Sync Pots + Contributions ──
        SyncPots(db, game, state);

        // ── Sync Betting Rounds + Actions ──
        SyncBettingRounds(db, game, state);

        await db.SaveChangesAsync(cancellationToken);

        // Capture new RowVersions back into runtime state
        state.LastCheckpointRowVersion = game.RowVersion ?? [];
        foreach (var gp in game.GamePlayers)
        {
            var runtimePlayer = state.Players.FirstOrDefault(p => p.Id == gp.Id);
            if (runtimePlayer is not null)
                runtimePlayer.LastCheckpointRowVersion = gp.RowVersion ?? [];
        }

        state.IsDirty = false;

        _logger.LogDebug("Checkpointed game {GameId} (version {Version})", state.GameId, state.Version);
    }

    private static void ApplyGameState(Game game, ActiveGameRuntimeState state)
    {
        game.GameTypeId = state.GameTypeId;
        game.CurrentPhase = state.CurrentPhase;
        game.CurrentHandNumber = state.CurrentHandNumber;
        game.Status = state.Status;
        game.DealerPosition = state.DealerPosition;
        game.CurrentPlayerIndex = state.CurrentPlayerIndex;
        game.BringInPlayerIndex = state.BringInPlayerIndex;
        game.CurrentDrawPlayerIndex = state.CurrentDrawPlayerIndex;
        game.Ante = state.Ante;
        game.SmallBlind = state.SmallBlind;
        game.BigBlind = state.BigBlind;
        game.BringIn = state.BringIn;
        game.SmallBet = state.SmallBet;
        game.BigBet = state.BigBet;
        game.MinBet = state.MinBet;
        game.MaxBuyIn = state.MaxBuyIn;
        game.RequiresJoinApproval = state.RequiresJoinApproval;
        game.GameSettings = state.GameSettings;
        game.IsDealersChoice = state.IsDealersChoice;
        game.AreOddsVisibleToAllPlayers = state.AreOddsVisibleToAllPlayers;
        game.CurrentHandGameTypeCode = state.CurrentHandGameTypeCode;
        game.DealersChoiceDealerPosition = state.DealersChoiceDealerPosition;
        game.OriginalDealersChoiceDealerPosition = state.OriginalDealersChoiceDealerPosition;
        game.IsPausedForChipCheck = state.IsPausedForChipCheck;
        game.ChipCheckPauseStartedAt = state.ChipCheckPauseStartedAt;
        game.ChipCheckPauseEndsAt = state.ChipCheckPauseEndsAt;
        game.UpdatedAt = state.UpdatedAt;
        game.StartedAt = state.StartedAt;
        game.EndedAt = state.EndedAt;
        game.HandCompletedAt = state.HandCompletedAt;
        game.NextHandStartsAt = state.NextHandStartsAt;
        game.DrawCompletedAt = state.DrawCompletedAt;
        game.RandomSeed = state.RandomSeed;
        game.UpdatedById = state.UpdatedById;
        game.UpdatedByName = state.UpdatedByName;
    }

    private static void SyncPlayers(CardsDbContext db, Game game, ActiveGameRuntimeState state)
    {
        var existingById = game.GamePlayers.ToDictionary(gp => gp.Id);
        var runtimeIds = new HashSet<Guid>(state.Players.Select(p => p.Id));

        // Remove players no longer in runtime state
        foreach (var gp in game.GamePlayers.Where(gp => !runtimeIds.Contains(gp.Id)).ToList())
        {
            db.GamePlayers.Remove(gp);
            game.GamePlayers.Remove(gp);
        }

        foreach (var rp in state.Players)
        {
            if (existingById.TryGetValue(rp.Id, out var gp))
            {
                // Update existing
                ApplyPlayerState(gp, rp);
            }
            else
            {
                // Insert new
                var newGp = new GamePlayer
                {
                    Id = rp.Id,
                    GameId = state.GameId,
                    PlayerId = rp.PlayerId,
                };
                ApplyPlayerState(newGp, rp);
                db.GamePlayers.Add(newGp);
                game.GamePlayers.Add(newGp);
            }
        }
    }

    private static void ApplyPlayerState(GamePlayer gp, RuntimeGamePlayer rp)
    {
        gp.SeatPosition = rp.SeatPosition;
        gp.ChipStack = rp.ChipStack;
        gp.StartingChips = rp.StartingChips;
        gp.CurrentBet = rp.CurrentBet;
        gp.TotalContributedThisHand = rp.TotalContributedThisHand;
        gp.HasFolded = rp.HasFolded;
        gp.IsAllIn = rp.IsAllIn;
        gp.IsConnected = rp.IsConnected;
        gp.IsSittingOut = rp.IsSittingOut;
        gp.DropOrStayDecision = rp.DropOrStayDecision;
        gp.AutoDropOnDropOrStay = rp.AutoDropOnDropOrStay;
        gp.HasDrawnThisRound = rp.HasDrawnThisRound;
        gp.JoinedAtHandNumber = rp.JoinedAtHandNumber;
        gp.LeftAtHandNumber = rp.LeftAtHandNumber;
        gp.FinalChipCount = rp.FinalChipCount;
        gp.PendingChipsToAdd = rp.PendingChipsToAdd;
        gp.BringInAmount = rp.BringInAmount;
        gp.Status = rp.Status;
        gp.VariantState = rp.VariantState;
        gp.JoinedAt = rp.JoinedAt;
        gp.LeftAt = rp.LeftAt;
    }

    private static void SyncCards(CardsDbContext db, Game game, ActiveGameRuntimeState state)
    {
        var existingById = game.GameCards.ToDictionary(c => c.Id);
        var runtimeIds = new HashSet<Guid>(state.Cards.Select(c => c.Id));

        foreach (var gc in game.GameCards.Where(c => !runtimeIds.Contains(c.Id)).ToList())
        {
            db.GameCards.Remove(gc);
            game.GameCards.Remove(gc);
        }

        foreach (var rc in state.Cards)
        {
            if (existingById.TryGetValue(rc.Id, out var gc))
            {
                ApplyCardState(gc, rc);
            }
            else
            {
                var newGc = new GameCard
                {
                    Id = rc.Id,
                    GameId = state.GameId,
                };
                ApplyCardState(newGc, rc);
                db.GameCards.Add(newGc);
                game.GameCards.Add(newGc);
            }
        }
    }

    private static void ApplyCardState(GameCard gc, RuntimeCard rc)
    {
        gc.GamePlayerId = rc.GamePlayerId;
        gc.HandNumber = rc.HandNumber;
        gc.Suit = rc.Suit;
        gc.Symbol = rc.Symbol;
        gc.Location = rc.Location;
        gc.DealOrder = rc.DealOrder;
        gc.DealtAtPhase = rc.DealtAtPhase;
        gc.IsVisible = rc.IsVisible;
        gc.IsWild = rc.IsWild;
        gc.IsDiscarded = rc.IsDiscarded;
        gc.DiscardedAtDrawRound = rc.DiscardedAtDrawRound;
        gc.IsDrawnCard = rc.IsDrawnCard;
        gc.DrawnAtRound = rc.DrawnAtRound;
        gc.IsBuyCard = rc.IsBuyCard;
        gc.DealtAt = rc.DealtAt;
    }

    private static void SyncPots(CardsDbContext db, Game game, ActiveGameRuntimeState state)
    {
        var existingById = game.Pots.ToDictionary(p => p.Id);
        var runtimeIds = new HashSet<Guid>(state.Pots.Select(p => p.Id));

        foreach (var pot in game.Pots.Where(p => !runtimeIds.Contains(p.Id)).ToList())
        {
            db.Pots.Remove(pot);
            game.Pots.Remove(pot);
        }

        foreach (var rp in state.Pots)
        {
            if (existingById.TryGetValue(rp.Id, out var pot))
            {
                ApplyPotState(pot, rp);
                SyncPotContributions(db, pot, rp);
            }
            else
            {
                var newPot = new Pot
                {
                    Id = rp.Id,
                    GameId = state.GameId,
                };
                ApplyPotState(newPot, rp);
                db.Pots.Add(newPot);
                game.Pots.Add(newPot);
                // Add contributions for new pot
                foreach (var rc in rp.Contributions)
                {
                    var newPc = new PotContribution
                    {
                        Id = rc.Id,
                        PotId = rp.Id,
                    };
                    ApplyContributionState(newPc, rc);
                    db.PotContributions.Add(newPc);
                    newPot.Contributions.Add(newPc);
                }
            }
        }
    }

    private static void ApplyPotState(Pot pot, RuntimePot rp)
    {
        pot.HandNumber = rp.HandNumber;
        pot.PotType = rp.PotType;
        pot.PotOrder = rp.PotOrder;
        pot.Amount = rp.Amount;
        pot.MaxContributionPerPlayer = rp.MaxContributionPerPlayer;
        pot.IsAwarded = rp.IsAwarded;
        pot.AwardedAt = rp.AwardedAt;
        pot.WinnerPayouts = rp.WinnerPayouts;
        pot.WinReason = rp.WinReason;
        pot.CreatedAt = rp.CreatedAt;
    }

    private static void SyncPotContributions(CardsDbContext db, Pot pot, RuntimePot rp)
    {
        var existingById = pot.Contributions.ToDictionary(c => c.Id);
        var runtimeIds = new HashSet<Guid>(rp.Contributions.Select(c => c.Id));

        foreach (var pc in pot.Contributions.Where(c => !runtimeIds.Contains(c.Id)).ToList())
        {
            db.PotContributions.Remove(pc);
            pot.Contributions.Remove(pc);
        }

        foreach (var rc in rp.Contributions)
        {
            if (existingById.TryGetValue(rc.Id, out var pc))
            {
                ApplyContributionState(pc, rc);
            }
            else
            {
                var newPc = new PotContribution
                {
                    Id = rc.Id,
                    PotId = pot.Id,
                };
                ApplyContributionState(newPc, rc);
                db.PotContributions.Add(newPc);
                pot.Contributions.Add(newPc);
            }
        }
    }

    private static void ApplyContributionState(PotContribution pc, RuntimePotContribution rc)
    {
        pc.GamePlayerId = rc.GamePlayerId;
        pc.Amount = rc.Amount;
        pc.IsEligibleToWin = rc.IsEligibleToWin;
        pc.IsPotMatch = rc.IsPotMatch;
        pc.ContributedAt = rc.ContributedAt;
    }

    private static void SyncBettingRounds(CardsDbContext db, Game game, ActiveGameRuntimeState state)
    {
        var existingById = game.BettingRounds.ToDictionary(br => br.Id);
        var runtimeIds = new HashSet<Guid>(state.BettingRounds.Select(br => br.Id));

        foreach (var br in game.BettingRounds.Where(br => !runtimeIds.Contains(br.Id)).ToList())
        {
            db.BettingRounds.Remove(br);
            game.BettingRounds.Remove(br);
        }

        foreach (var rbr in state.BettingRounds)
        {
            if (existingById.TryGetValue(rbr.Id, out var br))
            {
                ApplyBettingRoundState(br, rbr);
                SyncBettingActions(db, br, rbr);
            }
            else
            {
                var newBr = new BettingRound
                {
                    Id = rbr.Id,
                    GameId = state.GameId,
                    Street = rbr.Street,
                };
                ApplyBettingRoundState(newBr, rbr);
                db.BettingRounds.Add(newBr);
                game.BettingRounds.Add(newBr);
                // Add actions for new round
                foreach (var ra in rbr.Actions)
                {
                    var newBa = new BettingActionRecord
                    {
                        Id = ra.Id,
                        BettingRoundId = rbr.Id,
                    };
                    ApplyBettingActionState(newBa, ra);
                    db.BettingActionRecords.Add(newBa);
                    newBr.Actions.Add(newBa);
                }
            }
        }
    }

    private static void ApplyBettingRoundState(BettingRound br, RuntimeBettingRound rbr)
    {
        br.HandNumber = rbr.HandNumber;
        br.RoundNumber = rbr.RoundNumber;
        br.Street = rbr.Street;
        br.CurrentBet = rbr.CurrentBet;
        br.MinBet = rbr.MinBet;
        br.RaiseCount = rbr.RaiseCount;
        br.MaxRaises = rbr.MaxRaises;
        br.LastRaiseAmount = rbr.LastRaiseAmount;
        br.PlayersInHand = rbr.PlayersInHand;
        br.PlayersActed = rbr.PlayersActed;
        br.CurrentActorIndex = rbr.CurrentActorIndex;
        br.LastAggressorIndex = rbr.LastAggressorIndex;
        br.IsComplete = rbr.IsComplete;
        br.StartedAt = rbr.StartedAt;
        br.CompletedAt = rbr.CompletedAt;
    }

    private static void SyncBettingActions(CardsDbContext db, BettingRound br, RuntimeBettingRound rbr)
    {
        var existingById = br.Actions.ToDictionary(a => a.Id);
        var runtimeIds = new HashSet<Guid>(rbr.Actions.Select(a => a.Id));

        foreach (var ba in br.Actions.Where(a => !runtimeIds.Contains(a.Id)).ToList())
        {
            db.BettingActionRecords.Remove(ba);
            br.Actions.Remove(ba);
        }

        foreach (var ra in rbr.Actions)
        {
            if (existingById.TryGetValue(ra.Id, out var ba))
            {
                ApplyBettingActionState(ba, ra);
            }
            else
            {
                var newBa = new BettingActionRecord
                {
                    Id = ra.Id,
                    BettingRoundId = br.Id,
                };
                ApplyBettingActionState(newBa, ra);
                db.BettingActionRecords.Add(newBa);
                br.Actions.Add(newBa);
            }
        }
    }

    private static void ApplyBettingActionState(BettingActionRecord ba, RuntimeBettingAction ra)
    {
        ba.GamePlayerId = ra.GamePlayerId;
        ba.ActionOrder = ra.ActionOrder;
        ba.ActionType = ra.ActionType;
        ba.Amount = ra.Amount;
        ba.ChipsMoved = ra.ChipsMoved;
        ba.ChipStackBefore = ra.ChipStackBefore;
        ba.ChipStackAfter = ra.ChipStackAfter;
        ba.PotBefore = ra.PotBefore;
        ba.PotAfter = ra.PotAfter;
        ba.DecisionTimeSeconds = ra.DecisionTimeSeconds;
        ba.IsForced = ra.IsForced;
        ba.IsTimeout = ra.IsTimeout;
        ba.Note = ra.Note;
        ba.ActionAt = ra.ActionAt;
    }
}
