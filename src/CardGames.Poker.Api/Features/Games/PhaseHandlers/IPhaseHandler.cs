using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.PhaseHandlers;

/// <summary>
/// Handles game-specific phase logic for special phases that require unique behavior.
/// </summary>
/// <remarks>
/// <para>
/// Phase handlers are used for phases that have game-specific logic that cannot be
/// generalized across all poker variants. Examples include:
/// </para>
/// <list type="bullet">
///   <item><description>DropOrStay - Kings and Lows decision phase</description></item>
///   <item><description>PotMatching - Losers match the pot in Kings and Lows</description></item>
///   <item><description>PlayerVsDeck - Single player plays against the deck</description></item>
///   <item><description>BuyCardOffer - Baseball buy card mechanic</description></item>
/// </list>
/// <para>
/// Implementations should be stateless and thread-safe as they may be registered
/// as singletons and used across multiple concurrent games.
/// </para>
/// </remarks>
public interface IPhaseHandler
{
    /// <summary>
    /// Gets the phase ID this handler supports.
    /// </summary>
    /// <remarks>
    /// This should match the phase name from the <see cref="CardGames.Poker.Betting.Phases"/> enum.
    /// Examples: "DropOrStay", "PotMatching", "PlayerVsDeck", "BuyCardOffer"
    /// </remarks>
    string PhaseId { get; }

    /// <summary>
    /// Gets the game type codes this handler applies to.
    /// </summary>
    /// <remarks>
    /// An empty list means the handler applies to all games that have this phase.
    /// A non-empty list restricts the handler to specific game types.
    /// Examples: ["KINGSANDLOWS"], ["BASEBALL", "TWOSJACKSMANWITHTHEAXE"]
    /// </remarks>
    IReadOnlyList<string> ApplicableGameTypes { get; }

    /// <summary>
    /// Determines if all players have completed their actions for this phase.
    /// </summary>
    /// <param name="game">The game entity with current state.</param>
    /// <returns>True if the phase is complete and ready to transition; otherwise, false.</returns>
    /// <remarks>
    /// This is called after each player action to check if the phase should advance.
    /// For example, in DropOrStay, this returns true when all active players have decided.
    /// </remarks>
    bool IsPhaseComplete(Game game);

    /// <summary>
    /// Gets the next phase after this phase completes.
    /// </summary>
    /// <param name="game">The game entity with current state.</param>
    /// <returns>The name of the next phase to transition to.</returns>
    /// <remarks>
    /// The next phase may depend on game state. For example, after DropOrStay:
    /// - If all players dropped: Complete
    /// - If one player stayed: PlayerVsDeck
    /// - If multiple players stayed: DrawPhase
    /// </remarks>
    string GetNextPhase(Game game);

    /// <summary>
    /// Validates whether a player can take an action in this phase.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <param name="player">The player attempting to act.</param>
    /// <returns>Null if the action is valid; otherwise, an error message.</returns>
    string? ValidatePlayerCanAct(Game game, GamePlayer player);

    /// <summary>
    /// Gets available actions for a player in this phase.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <param name="player">The player to get actions for.</param>
    /// <returns>A list of available action names (e.g., ["Drop", "Stay"]).</returns>
    IReadOnlyList<string> GetAvailableActions(Game game, GamePlayer player);
}
