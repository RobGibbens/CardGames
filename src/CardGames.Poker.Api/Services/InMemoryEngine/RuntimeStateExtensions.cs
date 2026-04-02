using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Extension methods for querying and mutating <see cref="ActiveGameRuntimeState"/>.
/// These encapsulate the most common operations used by command handlers.
/// </summary>
public static class RuntimeStateExtensions
{
    // ── Player Queries ──

    /// <summary>
    /// Active players ordered by seat position.
    /// </summary>
    public static List<RuntimeGamePlayer> GetActivePlayersOrdered(this ActiveGameRuntimeState state) =>
        state.Players
            .Where(p => p.Status == GamePlayerStatus.Active)
            .OrderBy(p => p.SeatPosition)
            .ToList();

    /// <summary>
    /// Players eligible to play a new hand (active, not sitting out, sufficient chips for ante).
    /// </summary>
    public static List<RuntimeGamePlayer> GetEligiblePlayers(this ActiveGameRuntimeState state)
    {
        var minChips = state.Ante ?? 0;
        return state.Players
            .Where(p => p.Status == GamePlayerStatus.Active
                        && !p.IsSittingOut
                        && p.ChipStack >= minChips)
            .OrderBy(p => p.SeatPosition)
            .ToList();
    }

    /// <summary>
    /// Gets the player at the given seat position, or <c>null</c>.
    /// </summary>
    public static RuntimeGamePlayer? GetPlayerBySeat(this ActiveGameRuntimeState state, int seatPosition) =>
        state.Players.FirstOrDefault(p => p.SeatPosition == seatPosition);

    /// <summary>
    /// Gets a player by their <see cref="RuntimeGamePlayer.Id"/>.
    /// </summary>
    public static RuntimeGamePlayer? GetPlayerById(this ActiveGameRuntimeState state, Guid gamePlayerId) =>
        state.Players.FirstOrDefault(p => p.Id == gamePlayerId);

    /// <summary>
    /// Gets a player by their <see cref="RuntimeGamePlayer.PlayerId"/>.
    /// </summary>
    public static RuntimeGamePlayer? GetPlayerByPlayerId(this ActiveGameRuntimeState state, Guid playerId) =>
        state.Players.FirstOrDefault(p => p.PlayerId == playerId);

    /// <summary>
    /// Gets the player who must act next based on the active betting round's <c>CurrentActorIndex</c>.
    /// </summary>
    public static RuntimeGamePlayer? GetCurrentActor(this ActiveGameRuntimeState state)
    {
        var bettingRound = state.GetActiveBettingRound();
        if (bettingRound is null)
            return state.GetPlayerBySeat(state.CurrentPlayerIndex);

        return state.GetPlayerBySeat(bettingRound.CurrentActorIndex);
    }

    /// <summary>
    /// Players still in the current hand (not folded, status active or all-in).
    /// </summary>
    public static List<RuntimeGamePlayer> GetPlayersInHand(this ActiveGameRuntimeState state) =>
        state.Players
            .Where(p => p.Status == GamePlayerStatus.Active && !p.HasFolded)
            .OrderBy(p => p.SeatPosition)
            .ToList();

    /// <summary>
    /// Whether all remaining (non-folded) players are all-in.
    /// </summary>
    public static bool AreAllPlayersAllIn(this ActiveGameRuntimeState state) =>
        state.GetPlayersInHand().All(p => p.IsAllIn);

    // ── Betting Round Queries ──

    /// <summary>
    /// Gets the active (non-complete) betting round for the current hand,
    /// or <c>null</c> if none exists.
    /// </summary>
    public static RuntimeBettingRound? GetActiveBettingRound(this ActiveGameRuntimeState state) =>
        state.BettingRounds
            .Where(br => br.HandNumber == state.CurrentHandNumber && !br.IsComplete)
            .OrderByDescending(br => br.RoundNumber)
            .FirstOrDefault();

    /// <summary>
    /// Gets all betting rounds for the current hand, ordered by round number.
    /// </summary>
    public static List<RuntimeBettingRound> GetBettingRoundsForCurrentHand(this ActiveGameRuntimeState state) =>
        state.BettingRounds
            .Where(br => br.HandNumber == state.CurrentHandNumber)
            .OrderBy(br => br.RoundNumber)
            .ToList();

    // ── Pot Queries ──

    /// <summary>
    /// Gets the main pot for the current hand, or <c>null</c>.
    /// </summary>
    public static RuntimePot? GetMainPot(this ActiveGameRuntimeState state) =>
        state.Pots.FirstOrDefault(p =>
            p.HandNumber == state.CurrentHandNumber && p.PotOrder == 0);

    /// <summary>
    /// Gets the main pot for a specific hand number.
    /// </summary>
    public static RuntimePot? GetMainPotForHand(this ActiveGameRuntimeState state, int handNumber) =>
        state.Pots.FirstOrDefault(p => p.HandNumber == handNumber && p.PotOrder == 0);

    /// <summary>
    /// Total chips across all pots for the current hand.
    /// </summary>
    public static int GetTotalPotAmount(this ActiveGameRuntimeState state) =>
        state.Pots
            .Where(p => p.HandNumber == state.CurrentHandNumber)
            .Sum(p => p.Amount);

    // ── Card Queries ──

    /// <summary>
    /// Gets cards for a specific player in the current hand.
    /// </summary>
    public static List<RuntimeCard> GetPlayerCards(this ActiveGameRuntimeState state, Guid gamePlayerId) =>
        state.Cards
            .Where(c => c.GamePlayerId == gamePlayerId
                        && c.HandNumber == state.CurrentHandNumber
                        && !c.IsDiscarded)
            .OrderBy(c => c.DealOrder)
            .ToList();

    /// <summary>
    /// Gets community cards for the current hand.
    /// </summary>
    public static List<RuntimeCard> GetCommunityCards(this ActiveGameRuntimeState state) =>
        state.Cards
            .Where(c => c.Location == CardLocation.Community
                        && c.HandNumber == state.CurrentHandNumber)
            .OrderBy(c => c.DealOrder)
            .ToList();

    /// <summary>
    /// Gets all cards for the current hand (all locations, all players).
    /// </summary>
    public static List<RuntimeCard> GetCardsForCurrentHand(this ActiveGameRuntimeState state) =>
        state.Cards
            .Where(c => c.HandNumber == state.CurrentHandNumber)
            .ToList();

    // ── Mutations ──

    /// <summary>
    /// Resets per-hand state on all active players for a new hand.
    /// </summary>
    public static void ResetPlayerHandState(this ActiveGameRuntimeState state)
    {
        foreach (var player in state.Players.Where(p => p.Status == GamePlayerStatus.Active))
        {
            player.HasFolded = false;
            player.IsAllIn = false;
            player.CurrentBet = 0;
            player.TotalContributedThisHand = 0;
            player.HasDrawnThisRound = false;
            player.DropOrStayDecision = null;
            player.AutoDropOnDropOrStay = false;
        }
    }

    /// <summary>
    /// Creates a new main pot for the specified hand.
    /// </summary>
    public static RuntimePot CreateMainPot(this ActiveGameRuntimeState state, int handNumber, DateTimeOffset now)
    {
        var pot = new RuntimePot
        {
            Id = Guid.CreateVersion7(),
            GameId = state.GameId,
            HandNumber = handNumber,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = 0,
            CreatedAt = now,
        };
        state.Pots.Add(pot);
        return pot;
    }

    /// <summary>
    /// Creates a new betting round for the current hand.
    /// </summary>
    public static RuntimeBettingRound CreateBettingRound(
        this ActiveGameRuntimeState state,
        string street,
        int roundNumber,
        int minBet,
        int maxRaises,
        int playersInHand,
        int firstActorIndex,
        DateTimeOffset now)
    {
        var round = new RuntimeBettingRound
        {
            Id = Guid.CreateVersion7(),
            GameId = state.GameId,
            HandNumber = state.CurrentHandNumber,
            RoundNumber = roundNumber,
            Street = street,
            CurrentBet = 0,
            MinBet = minBet,
            MaxRaises = maxRaises,
            PlayersInHand = playersInHand,
            CurrentActorIndex = firstActorIndex,
            StartedAt = now,
        };
        state.BettingRounds.Add(round);
        return round;
    }

    /// <summary>
    /// Records a betting action and updates player/round state.
    /// </summary>
    public static RuntimeBettingAction RecordBettingAction(
        this RuntimeBettingRound round,
        RuntimeGamePlayer player,
        BettingActionType actionType,
        int amount,
        int chipsMoved,
        int potBefore,
        int potAfter,
        DateTimeOffset now,
        bool isForced = false,
        bool isTimeout = false,
        string? note = null)
    {
        var action = new RuntimeBettingAction
        {
            Id = Guid.CreateVersion7(),
            BettingRoundId = round.Id,
            GamePlayerId = player.Id,
            ActionOrder = round.Actions.Count + 1,
            ActionType = actionType,
            Amount = amount,
            ChipsMoved = chipsMoved,
            ChipStackBefore = player.ChipStack + chipsMoved, // before deduction
            ChipStackAfter = player.ChipStack,
            PotBefore = potBefore,
            PotAfter = potAfter,
            IsForced = isForced,
            IsTimeout = isTimeout,
            Note = note,
            ActionAt = now,
        };
        round.Actions.Add(action);
        round.PlayersActed++;
        return action;
    }

    /// <summary>
    /// Finds the next active (non-folded, non-all-in) player seat index
    /// after the given seat position, wrapping around.
    /// Returns -1 if no eligible player is found.
    /// </summary>
    public static int FindNextActivePlayerIndex(
        this ActiveGameRuntimeState state,
        int currentSeatPosition)
    {
        var activePlayers = state.Players
            .Where(p => p.Status == GamePlayerStatus.Active && !p.HasFolded && !p.IsAllIn)
            .OrderBy(p => p.SeatPosition)
            .ToList();

        if (activePlayers.Count == 0)
            return -1;

        // Find first player with seat > current, or wrap to first seat
        var next = activePlayers.FirstOrDefault(p => p.SeatPosition > currentSeatPosition);
        return next?.SeatPosition ?? activePlayers[0].SeatPosition;
    }
}
