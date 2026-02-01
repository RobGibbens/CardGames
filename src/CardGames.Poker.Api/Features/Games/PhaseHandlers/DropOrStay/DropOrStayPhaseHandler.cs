using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.PhaseHandlers.DropOrStay;

/// <summary>
/// Handles the Drop or Stay phase for Kings and Lows poker variant.
/// </summary>
/// <remarks>
/// <para>
/// In Kings and Lows, after cards are dealt, each player must decide to either:
/// </para>
/// <list type="bullet">
///   <item><description>Stay - Continue playing and compete for the pot</description></item>
///   <item><description>Drop - Fold and exit the hand</description></item>
/// </list>
/// <para>
/// All players make their decisions simultaneously. Once all decisions are recorded,
/// the game transitions to the next phase based on how many players stayed:
/// </para>
/// <list type="bullet">
///   <item><description>0 players stayed: Hand ends (Complete)</description></item>
///   <item><description>1 player stayed: Player vs Deck mode</description></item>
///   <item><description>2+ players stayed: Normal draw phase</description></item>
/// </list>
/// </remarks>
public sealed class DropOrStayPhaseHandler : IPhaseHandler
{
    /// <inheritdoc />
    public string PhaseId => nameof(Phases.DropOrStay);

    /// <inheritdoc />
    public IReadOnlyList<string> ApplicableGameTypes => ["KINGSANDLOWS"];

    /// <inheritdoc />
    public bool IsPhaseComplete(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var activePlayers = GetActivePlayers(game);

        // Phase is complete when all active players have made a decision
        return activePlayers.All(p =>
            p.DropOrStayDecision.HasValue &&
            p.DropOrStayDecision.Value != Data.Entities.DropOrStayDecision.Undecided);
    }

    /// <inheritdoc />
    public string GetNextPhase(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var stayingPlayerCount = GetStayingPlayerCount(game);

        return stayingPlayerCount switch
        {
            0 => nameof(Phases.Showdown), // All dropped - go to showdown for pot handling
            1 => nameof(Phases.DrawPhase), // Single player - draw phase then PlayerVsDeck
            _ => nameof(Phases.DrawPhase) // Multiple players - normal draw phase
        };
    }

    /// <inheritdoc />
    public string? ValidatePlayerCanAct(Game game, GamePlayer player)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(player);

        // Check game is in correct phase
        if (game.CurrentPhase != nameof(Phases.DropOrStay))
        {
            return $"Cannot make drop/stay decision. Game is in '{game.CurrentPhase}' phase, " +
                   $"but must be in '{nameof(Phases.DropOrStay)}' phase.";
        }

        // Check player is active in the game
        if (player.Status != GamePlayerStatus.Active)
        {
            return "Player is not active in this game.";
        }

        // Check player is not sitting out
        if (player.IsSittingOut)
        {
            return "Player is sitting out and cannot make decisions.";
        }

        // Check player hasn't folded
        if (player.HasFolded)
        {
            return "Player has already folded.";
        }

        // Check if player has already decided
        if (player.DropOrStayDecision.HasValue &&
            player.DropOrStayDecision.Value != Data.Entities.DropOrStayDecision.Undecided)
        {
            return "Player has already made their decision.";
        }

        return null; // No validation error
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableActions(Game game, GamePlayer player)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(player);

        // If player can't act, return empty list
        if (ValidatePlayerCanAct(game, player) is not null)
        {
            return [];
        }

        // Check if player is flagged for auto-drop (from chip check timeout)
        if (player.AutoDropOnDropOrStay)
        {
            return ["Drop"]; // Only drop is available
        }

        return ["Drop", "Stay"];
    }

    /// <summary>
    /// Determines the seat position of the first player to act in the draw phase.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The seat position of the first drawer.</returns>
    /// <remarks>
    /// The first drawer is the first staying player after the dealer, going clockwise.
    /// </remarks>
    public int DetermineFirstDrawerSeatPosition(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var stayingPlayers = game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active, HasFolded: false } &&
                         gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
            .OrderBy(gp => gp.SeatPosition)
            .ToList();

        if (stayingPlayers.Count == 0)
        {
            return 0;
        }

        var dealerPosition = game.DealerPosition;
        var totalPlayers = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active)
            .OrderBy(gp => gp.SeatPosition)
            .ToList();

        // Find the first staying player clockwise from dealer
        var dealerIndex = totalPlayers.FindIndex(gp => gp.SeatPosition == dealerPosition);
        if (dealerIndex < 0)
        {
            dealerIndex = 0;
        }

        var searchIndex = (dealerIndex + 1) % totalPlayers.Count;
        for (var i = 0; i < totalPlayers.Count; i++)
        {
            var candidate = totalPlayers[searchIndex];
            if (candidate.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
            {
                return candidate.SeatPosition;
            }
            searchIndex = (searchIndex + 1) % totalPlayers.Count;
        }

        // Fallback to first staying player
        return stayingPlayers[0].SeatPosition;
    }

    /// <summary>
    /// Gets the count of players who chose to stay.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The number of players who stayed.</returns>
    public int GetStayingPlayerCount(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return game.GamePlayers
            .Count(gp => gp is { Status: GamePlayerStatus.Active, HasFolded: false } &&
                         gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay);
    }

    /// <summary>
    /// Gets players who chose to drop.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>List of players who dropped.</returns>
    public IReadOnlyList<GamePlayer> GetDroppedPlayers(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active } &&
                         gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Drop)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets players who chose to stay.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>List of players who stayed.</returns>
    public IReadOnlyList<GamePlayer> GetStayingPlayers(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        return game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active, HasFolded: false } &&
                         gp.DropOrStayDecision == Data.Entities.DropOrStayDecision.Stay)
            .OrderBy(gp => gp.SeatPosition)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Processes any pending auto-drops from chip check pause timeout.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The number of players that were auto-dropped.</returns>
    public int ProcessPendingAutoDrops(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);

        var autoDropCount = 0;

        var pendingAutoDrops = game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active, IsSittingOut: false, HasFolded: false } &&
                         gp.AutoDropOnDropOrStay &&
                         (!gp.DropOrStayDecision.HasValue ||
                          gp.DropOrStayDecision.Value == Data.Entities.DropOrStayDecision.Undecided))
            .ToList();

        foreach (var player in pendingAutoDrops)
        {
            player.DropOrStayDecision = Data.Entities.DropOrStayDecision.Drop;
            player.HasFolded = true;
            player.AutoDropOnDropOrStay = false; // Clear the flag
            autoDropCount++;
        }

        return autoDropCount;
    }

    /// <summary>
    /// Gets the active players who can make a drop/stay decision.
    /// </summary>
    private static IEnumerable<GamePlayer> GetActivePlayers(Game game)
    {
        return game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active, IsSittingOut: false, HasFolded: false });
    }
}
