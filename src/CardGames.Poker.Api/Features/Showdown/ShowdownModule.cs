namespace CardGames.Poker.Api.Features.Showdown;

/// <summary>
/// Extension methods for registering showdown services.
/// </summary>
public static class ShowdownModule
{
    /// <summary>
    /// Adds showdown coordinator services to the service collection.
    /// </summary>
    public static IServiceCollection AddShowdownServices(this IServiceCollection services)
    {
        services.AddSingleton<IShowdownCoordinator, ShowdownCoordinator>();
        services.AddSingleton<IShowdownAuditLogger, InMemoryShowdownAuditLogger>();
        
        return services;
    }
}
