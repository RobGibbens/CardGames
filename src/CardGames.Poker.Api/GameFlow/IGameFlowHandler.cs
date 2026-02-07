using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Handles game-specific flow logic for poker variants.
/// Each game variant implements this to customize phase transitions,
/// dealing patterns, and showdown behavior.
/// </summary>
/// <remarks>
/// <para>
/// This interface is part of the Generic Command Handler Architecture that allows
/// poker variants to share common command handlers while encapsulating game-specific
/// logic in strategy implementations.
/// </para>
/// <para>
/// Implementations should be stateless and thread-safe as they may be registered
/// as singletons and used across multiple concurrent games.
/// </para>
/// </remarks>
public interface IGameFlowHandler
{
    /// <summary>
    /// Gets the game type code this handler supports.
    /// </summary>
    /// <remarks>
    /// This should match the <see cref="GameType.Code"/> value in the database.
    /// Examples: "FIVECARDDRAW", "SEVENCARDSTUD", "KINGSANDLOWS"
    /// </remarks>
    string GameTypeCode { get; }

    /// <summary>
    /// Gets the game rules for this variant.
    /// </summary>
    /// <returns>The <see cref="GameRules"/> describing this game's flow and mechanics.</returns>
    GameRules GetGameRules();

    /// <summary>
    /// Determines the initial phase after starting a new hand.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The phase name to transition to.</returns>
    /// <remarks>
    /// Most games start with "CollectingAntes", but some variants like Kings and Lows
    /// may start with "Dealing" and handle ante collection differently.
    /// </remarks>
    string GetInitialPhase(Game game);

    /// <summary>
    /// Determines the next phase after the current phase completes.
    /// </summary>
    /// <param name="game">The game entity with current state.</param>
    /// <param name="currentPhase">The current phase name.</param>
    /// <returns>The next phase name, or null if no automatic transition is needed.</returns>
    /// <remarks>
    /// Some phase transitions depend on game state (e.g., number of remaining players).
    /// Return null if the transition should be handled by a command handler explicitly.
    /// </remarks>
    string? GetNextPhase(Game game, string currentPhase);

    /// <summary>
    /// Gets the dealing configuration for this game.
    /// </summary>
    /// <returns>A <see cref="DealingConfiguration"/> describing how cards are dealt.</returns>
    DealingConfiguration GetDealingConfiguration();

    /// <summary>
    /// Determines if the game should skip ante collection phase.
    /// </summary>
    /// <remarks>
    /// Some games (e.g., Kings and Lows) collect antes during a different phase
    /// such as DropOrStay, rather than having a dedicated CollectingAntes phase.
    /// </remarks>
    bool SkipsAnteCollection { get; }

    /// <summary>
    /// Gets phases that are unique to this game variant and require special handling.
    /// </summary>
    /// <remarks>
    /// Examples: "DropOrStay", "PotMatching", "PlayerVsDeck", "BuyCardOffer"
    /// These phases typically have their own command handlers rather than using generic ones.
    /// </remarks>
    IReadOnlyList<string> SpecialPhases { get; }

    /// <summary>
    /// Performs any game-specific initialization when starting a new hand.
    /// </summary>
    /// <param name="game">The game entity being started.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Use this to reset game-specific player state, clear variant-specific flags,
    /// or perform any setup required before the first phase begins.
    /// </remarks>
    Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an automatic action for a player or the game when a timer expires.
    /// </summary>
    /// <param name="context">The context for the auto action.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PerformAutoActionAsync(AutoActionContext context);

    /// <summary>
    /// Performs any cleanup or finalization when a hand is completed.
    /// </summary>
    /// <param name="game">The game entity that just completed.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Use this to finalize game-specific state, record history,
    /// or prepare for the next hand.
    /// </remarks>
    Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default);

    #region Dealing

    /// <summary>
    /// Deals cards to players for a new hand.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="eligiblePlayers">Players to receive cards.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Override this in handlers that require non-standard dealing patterns
    /// (e.g., Seven Card Stud with street-based dealing).
    /// </remarks>
    Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    #endregion

    #region Showdown

    /// <summary>
    /// Gets whether this game supports inline showdown processing by the background service.
    /// </summary>
    /// <remarks>
    /// When true, the background service can call <see cref="PerformShowdownAsync"/>
    /// directly during phase transitions (e.g., after DrawComplete in Kings and Lows).
    /// When false, showdown is handled exclusively by the PerformShowdownCommandHandler.
    /// </remarks>
    bool SupportsInlineShowdown { get; }

    /// <summary>
    /// Performs showdown evaluation, pot distribution, and state updates.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="handHistoryRecorder">Service for recording hand history.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    /// <remarks>
    /// Only called by the background service when <see cref="SupportsInlineShowdown"/> is true.
    /// Most games should use the generic PerformShowdownCommandHandler instead.
    /// </remarks>
    Task<ShowdownResult> PerformShowdownAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    #endregion

    #region Post-Phase Processing

    /// <summary>
    /// Processes the DrawComplete phase transition.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="handHistoryRecorder">Service for recording hand history.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next phase to transition to.</returns>
    /// <remarks>
    /// Called when a draw phase has completed and all players have drawn.
    /// Default behavior transitions to SecondBettingRound.
    /// Override for games with different post-draw behavior.
    /// </remarks>
    Task<string> ProcessDrawCompleteAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Processes any post-showdown actions.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="showdownResult">Results from the showdown.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next phase to transition to.</returns>
    /// <remarks>
    /// Used for games that have additional mechanics after showdown,
    /// such as pot matching in Kings and Lows.
    /// </remarks>
    Task<string> ProcessPostShowdownAsync(
        CardsDbContext context,
        Game game,
        ShowdownResult showdownResult,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    #endregion

    #region Chip Check

    /// <summary>
    /// Gets whether this game requires chip coverage check before starting new hands.
    /// </summary>
    /// <remarks>
    /// Games like Kings and Lows require all players to be able to cover the pot
    /// before a new hand can start. If a player cannot cover, the game pauses
    /// for a configurable period to allow chip additions.
    /// </remarks>
    bool RequiresChipCoverageCheck { get; }

    /// <summary>
    /// Gets the chip check configuration for this game type.
    /// </summary>
    /// <returns>Configuration specifying pause duration and behavior.</returns>
    ChipCheckConfiguration GetChipCheckConfiguration();

    #endregion
}
