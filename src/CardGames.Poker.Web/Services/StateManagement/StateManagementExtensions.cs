namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Extension methods for registering state management services.
/// </summary>
public static class StateManagementExtensions
{
    /// <summary>
    /// Adds state management services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStateManagement(this IServiceCollection services)
    {
        // Register core state management services
        services.AddScoped<IGameStateManager, GameStateManager>();
        services.AddScoped<IEventReplayService, EventReplayService>();
        services.AddScoped<IStateValidationService, StateValidationService>();
        services.AddScoped<IConcurrentActionHandler, ConcurrentActionHandler>();

        // Register the coordinator that ties everything together
        services.AddScoped<IStateSyncCoordinator, StateSyncCoordinator>();

        return services;
    }
}
