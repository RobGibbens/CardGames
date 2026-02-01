using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Base implementation of <see cref="IGameFlowHandler"/> with common poker game logic.
/// Game-specific handlers inherit from this and override specific behaviors.
/// </summary>
/// <remarks>
/// <para>
/// This base class provides default implementations for common poker operations:
/// </para>
/// <list type="bullet">
///   <item><description>Initial phase transitions (defaults to CollectingAntes)</description></item>
///   <item><description>Sequential phase navigation based on GameRules.Phases</description></item>
///   <item><description>Helper methods for phase categorization</description></item>
/// </list>
/// <para>
/// Derived classes should override specific methods to customize game-specific behavior
/// while inheriting common logic.
/// </para>
/// </remarks>
public abstract class BaseGameFlowHandler : IGameFlowHandler
{
    /// <inheritdoc />
    public abstract string GameTypeCode { get; }

    /// <inheritdoc />
    public abstract GameRules GetGameRules();

    /// <inheritdoc />
    public virtual string GetInitialPhase(Game game)
    {
        // Default: Start with ante collection
        return nameof(Phases.CollectingAntes);
    }

    /// <inheritdoc />
    public virtual string? GetNextPhase(Game game, string currentPhase)
    {
        var rules = GetGameRules();
        var phases = rules.Phases;

        // Find the current phase index
        var currentIndex = -1;
        for (var i = 0; i < phases.Count; i++)
        {
            if (string.Equals(phases[i].PhaseId, currentPhase, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        // If current phase not found or is the last phase, no transition
        if (currentIndex < 0 || currentIndex >= phases.Count - 1)
        {
            return null;
        }

        return phases[currentIndex + 1].PhaseId;
    }

    /// <inheritdoc />
    public abstract DealingConfiguration GetDealingConfiguration();

    /// <inheritdoc />
    public virtual bool SkipsAnteCollection => false;

    /// <inheritdoc />
    public virtual IReadOnlyList<string> SpecialPhases => [];

    /// <inheritdoc />
    public virtual Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Default: No special initialization
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Default: No special cleanup
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if the specified phase is a betting phase.
    /// </summary>
    /// <param name="phase">The phase name to check.</param>
    /// <returns>True if the phase is categorized as Betting; otherwise, false.</returns>
    protected bool IsBettingPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));

        return string.Equals(phaseDescriptor?.Category, "Betting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the specified phase is a drawing phase.
    /// </summary>
    /// <param name="phase">The phase name to check.</param>
    /// <returns>True if the phase is categorized as Drawing; otherwise, false.</returns>
    protected bool IsDrawingPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));

        return string.Equals(phaseDescriptor?.Category, "Drawing", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the specified phase is a resolution phase (Showdown, Complete).
    /// </summary>
    /// <param name="phase">The phase name to check.</param>
    /// <returns>True if the phase is categorized as Resolution; otherwise, false.</returns>
    protected bool IsResolutionPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));

        return string.Equals(phaseDescriptor?.Category, "Resolution", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the phase descriptor for the specified phase ID.
    /// </summary>
    /// <param name="phaseId">The phase ID to look up.</param>
    /// <returns>The phase descriptor, or null if not found.</returns>
    protected GamePhaseDescriptor? GetPhaseDescriptor(string phaseId)
    {
        var rules = GetGameRules();
        return rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phaseId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Counts the number of active players who haven't folded.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The count of active, non-folded players.</returns>
    protected static int CountActivePlayers(Game game)
    {
        return game.GamePlayers
            .Count(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded && !gp.IsSittingOut);
    }

    /// <summary>
    /// Determines if only one player remains active (potential early win).
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>True if only one active player remains; otherwise, false.</returns>
    protected static bool IsSinglePlayerRemaining(Game game)
    {
        return CountActivePlayers(game) == 1;
    }
}
