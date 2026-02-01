using System.Collections.Frozen;
using System.Reflection;

namespace CardGames.Poker.Api.Features.Games.PhaseHandlers;

/// <summary>
/// Factory for creating phase-specific handlers.
/// Uses assembly scanning to discover all <see cref="IPhaseHandler"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// The factory maintains a registry of phase handlers keyed by phase ID and game type.
/// When a handler specifies applicable game types, it is only matched for those types.
/// When a handler has an empty applicable game types list, it serves as a fallback
/// for any game type that doesn't have a specific handler.
/// </para>
/// </remarks>
public sealed class PhaseHandlerFactory : IPhaseHandlerFactory
{
    private readonly FrozenDictionary<string, List<IPhaseHandler>> _handlersByPhase;
    private readonly IReadOnlyList<IPhaseHandler> _allHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseHandlerFactory"/> class.
    /// </summary>
    /// <remarks>
    /// Scans the executing assembly for all <see cref="IPhaseHandler"/> implementations
    /// and registers them by phase ID.
    /// </remarks>
    public PhaseHandlerFactory()
    {
        var handlers = new List<IPhaseHandler>();
        var handlersByPhase = new Dictionary<string, List<IPhaseHandler>>(StringComparer.OrdinalIgnoreCase);

        // Discover all IPhaseHandler implementations via reflection
        var handlerInterface = typeof(IPhaseHandler);
        var assembly = Assembly.GetExecutingAssembly();

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && handlerInterface.IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                if (Activator.CreateInstance(handlerType) is IPhaseHandler handler)
                {
                    handlers.Add(handler);

                    if (!handlersByPhase.TryGetValue(handler.PhaseId, out var phaseHandlers))
                    {
                        phaseHandlers = [];
                        handlersByPhase[handler.PhaseId] = phaseHandlers;
                    }

                    phaseHandlers.Add(handler);
                }
            }
            catch
            {
                // Skip handlers that can't be instantiated
            }
        }

        _allHandlers = handlers.AsReadOnly();
        _handlersByPhase = handlersByPhase.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseHandlerFactory"/> class
    /// with explicitly provided handlers.
    /// </summary>
    /// <param name="handlers">The handlers to register.</param>
    /// <remarks>
    /// This constructor is useful for testing and dependency injection scenarios.
    /// </remarks>
    public PhaseHandlerFactory(IEnumerable<IPhaseHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var handlerList = handlers.ToList();
        var handlersByPhase = new Dictionary<string, List<IPhaseHandler>>(StringComparer.OrdinalIgnoreCase);

        foreach (var handler in handlerList)
        {
            if (!handlersByPhase.TryGetValue(handler.PhaseId, out var phaseHandlers))
            {
                phaseHandlers = [];
                handlersByPhase[handler.PhaseId] = phaseHandlers;
            }

            phaseHandlers.Add(handler);
        }

        _allHandlers = handlerList.AsReadOnly();
        _handlersByPhase = handlersByPhase.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IPhaseHandler GetHandler(string phaseId, string gameTypeCode)
    {
        if (!TryGetHandler(phaseId, gameTypeCode, out var handler))
        {
            throw new InvalidOperationException(
                $"No phase handler found for phase '{phaseId}' and game type '{gameTypeCode}'.");
        }

        return handler!;
    }

    /// <inheritdoc />
    public bool TryGetHandler(string phaseId, string gameTypeCode, out IPhaseHandler? handler)
    {
        handler = null;

        if (string.IsNullOrWhiteSpace(phaseId))
        {
            return false;
        }

        if (!_handlersByPhase.TryGetValue(phaseId, out var phaseHandlers))
        {
            return false;
        }

        // First, try to find a handler specific to this game type
        handler = phaseHandlers.FirstOrDefault(h =>
            h.ApplicableGameTypes.Count > 0 &&
            h.ApplicableGameTypes.Contains(gameTypeCode, StringComparer.OrdinalIgnoreCase));

        // If no game-specific handler found, try to find a universal handler (empty ApplicableGameTypes)
        handler ??= phaseHandlers.FirstOrDefault(h => h.ApplicableGameTypes.Count == 0);

        return handler is not null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IPhaseHandler> GetAllHandlers()
    {
        return _allHandlers;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IPhaseHandler> GetHandlersForGameType(string gameTypeCode)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            return [];
        }

        var applicableHandlers = new List<IPhaseHandler>();

        foreach (var (_, handlers) in _handlersByPhase)
        {
            // Find the best handler for this game type in each phase
            var handler = handlers.FirstOrDefault(h =>
                h.ApplicableGameTypes.Count > 0 &&
                h.ApplicableGameTypes.Contains(gameTypeCode, StringComparer.OrdinalIgnoreCase));

            handler ??= handlers.FirstOrDefault(h => h.ApplicableGameTypes.Count == 0);

            if (handler is not null)
            {
                applicableHandlers.Add(handler);
            }
        }

        return applicableHandlers.AsReadOnly();
    }
}
