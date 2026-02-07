using System.Collections.Frozen;
using System.Reflection;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Factory for creating game-specific flow handlers.
/// Uses assembly scanning to discover all <see cref="IGameFlowHandler"/> implementations.
/// </summary>
/// <remarks>
/// <para>
/// This factory automatically discovers all concrete implementations of <see cref="IGameFlowHandler"/>
/// in the executing assembly using reflection. Handlers are cached in a frozen dictionary
/// for optimal runtime performance.
/// </para>
/// <para>
/// The factory provides a default fallback handler (Five Card Draw) for unknown game types
/// to ensure the system remains functional even with missing handler registrations.
/// </para>
/// <para>
/// This pattern follows the same approach as <c>PokerGameMetadataRegistry</c> and
/// <c>HandEvaluatorFactory</c> for consistent extensibility across the codebase.
/// </para>
/// </remarks>
public sealed class GameFlowHandlerFactory : IGameFlowHandlerFactory
{
    private readonly FrozenDictionary<string, IGameFlowHandler> _handlers;
    private readonly IGameFlowHandler _defaultHandler;
    private readonly ILogger<GameFlowHandlerFactory>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameFlowHandlerFactory"/> class.
    /// </summary>
    /// <remarks>
    /// Scans the executing assembly for all <see cref="IGameFlowHandler"/> implementations
    /// and registers them by their <see cref="IGameFlowHandler.GameTypeCode"/>.
    /// </remarks>
    public GameFlowHandlerFactory() : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameFlowHandlerFactory"/> class with logging.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public GameFlowHandlerFactory(ILogger<GameFlowHandlerFactory>? logger)
    {
        _logger = logger;
        var handlersDict = new Dictionary<string, IGameFlowHandler>(StringComparer.OrdinalIgnoreCase);

        // Create default handler first
        _defaultHandler = new FiveCardDrawFlowHandler();
        handlersDict[_defaultHandler.GameTypeCode] = _defaultHandler;

        // Discover all IGameFlowHandler implementations via reflection
        var handlerInterface = typeof(IGameFlowHandler);
        var assembly = Assembly.GetExecutingAssembly();

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && handlerInterface.IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                if (Activator.CreateInstance(handlerType) is IGameFlowHandler handler)
                {
                    handlersDict[handler.GameTypeCode] = handler;
                    _logger?.LogDebug(
                        "Registered flow handler {HandlerType} for game type {GameTypeCode}",
                        handlerType.Name,
                        handler.GameTypeCode);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Failed to instantiate flow handler {HandlerType}. Skipping.",
                    handlerType.Name);
            }
        }

        _handlers = handlersDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        _logger?.LogInformation(
            "GameFlowHandlerFactory initialized with {HandlerCount} handlers: {GameTypes}",
            _handlers.Count,
            string.Join(", ", _handlers.Keys));
    }

    /// <inheritdoc />
    public IGameFlowHandler GetHandler(string? gameTypeCode)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            _logger?.LogDebug(
                "Null or empty game type code provided. Returning default handler ({DefaultType}).",
                _defaultHandler.GameTypeCode);
            return _defaultHandler;
        }

        if (_handlers.TryGetValue(gameTypeCode, out var handler))
        {
            return handler;
        }

        _logger?.LogWarning(
            "No handler found for game type {GameTypeCode}. Returning default handler ({DefaultType}).",
            gameTypeCode,
            _defaultHandler.GameTypeCode);
        return _defaultHandler;
    }

    /// <inheritdoc />
    public bool TryGetHandler(string? gameTypeCode, out IGameFlowHandler? handler)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            handler = null;
            return false;
        }

        return _handlers.TryGetValue(gameTypeCode, out handler);
    }

    /// <summary>
    /// Gets all registered game type codes.
    /// </summary>
    /// <returns>A collection of all registered game type codes.</returns>
    public IReadOnlyCollection<string> GetRegisteredGameTypes() => _handlers.Keys;

    /// <summary>
    /// Gets the count of registered handlers.
    /// </summary>
    public int HandlerCount => _handlers.Count;
}
