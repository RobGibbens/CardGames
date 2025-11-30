using CardGames.Poker.Games;
using CardGames.Poker.Variants;

namespace CardGames.Poker.Api.Features.Variants;

/// <summary>
/// Extension methods for registering game variants with dependency injection.
/// </summary>
public static class VariantServiceExtensions
{
    /// <summary>
    /// Adds the game variant factory and provider services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGameVariantFactory(this IServiceCollection services)
    {
        // Register the registry as a singleton
        services.AddSingleton<GameVariantRegistry>();

        // Register the factory (which also implements IGameVariantProvider)
        services.AddSingleton<GameVariantFactory>();
        services.AddSingleton<IGameVariantFactory>(sp => sp.GetRequiredService<GameVariantFactory>());
        services.AddSingleton<IGameVariantProvider>(sp => sp.GetRequiredService<GameVariantFactory>());

        return services;
    }

    /// <summary>
    /// Registers the built-in poker variants (Texas Hold'em, Omaha).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBuiltInVariants(this IServiceCollection services)
    {
        // Use a hosted service to register variants after all services are configured
        services.AddHostedService<BuiltInVariantRegistrationService>();
        return services;
    }

    /// <summary>
    /// Registers a custom game variant.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="info">The variant metadata.</param>
    /// <param name="factory">The factory delegate to create game instances.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGameVariant(
        this IServiceCollection services,
        GameVariantInfo info,
        GameCreationDelegate factory)
    {
        services.AddSingleton(new VariantRegistrationRequest(info, factory));
        return services;
    }
}

/// <summary>
/// Internal class to hold variant registration requests.
/// </summary>
internal record VariantRegistrationRequest(GameVariantInfo Info, GameCreationDelegate Factory);

/// <summary>
/// Hosted service that registers built-in variants on startup.
/// </summary>
internal class BuiltInVariantRegistrationService : IHostedService
{
    private readonly GameVariantRegistry _registry;
    private readonly IEnumerable<VariantRegistrationRequest> _registrationRequests;

    public BuiltInVariantRegistrationService(
        GameVariantRegistry registry,
        IEnumerable<VariantRegistrationRequest> registrationRequests)
    {
        _registry = registry;
        _registrationRequests = registrationRequests;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register built-in variants
        RegisterTexasHoldem();
        RegisterOmaha();
        RegisterSevenCardStud();
        RegisterFiveCardDraw();
        RegisterFollowTheQueen();
        RegisterBaseball();

        // Register any custom variants added via AddGameVariant
        foreach (var request in _registrationRequests)
        {
            _registry.RegisterVariant(request.Info, request.Factory);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void RegisterTexasHoldem()
    {
        var info = new GameVariantInfo(
            Id: "texas-holdem",
            Name: "Texas Hold'em",
            Description: "The most popular poker variant. Each player receives 2 hole cards and uses them with 5 community cards to make the best 5-card hand.",
            MinPlayers: 2,
            MaxPlayers: 10);

        _registry.RegisterVariant(info, (players, smallBlind, bigBlind) =>
            new HoldEmGame(players, smallBlind, bigBlind));
    }

    private void RegisterOmaha()
    {
        var info = new GameVariantInfo(
            Id: "omaha",
            Name: "Omaha",
            Description: "Omaha poker. Each player receives 4 hole cards and must use exactly 2 of them with exactly 3 community cards to make the best 5-card hand.",
            MinPlayers: 2,
            MaxPlayers: 10);

        _registry.RegisterVariant(info, (players, smallBlind, bigBlind) =>
            new OmahaGame(players, smallBlind, bigBlind));
    }

    private void RegisterSevenCardStud()
    {
        var info = new GameVariantInfo(
            Id: "seven-card-stud",
            Name: "Seven Card Stud",
            Description: "A classic stud poker variant. Each player receives 7 cards (3 face-down, 4 face-up) and makes the best 5-card hand. Uses antes and bring-in instead of blinds.",
            MinPlayers: 2,
            MaxPlayers: 7);

        // Map smallBlind to ante and bigBlind to smallBet
        // bringIn is set to half of smallBet, bigBet is double smallBet
        _registry.RegisterVariant(info, (players, smallBlind, bigBlind) =>
            new SevenCardStudGame(
                players,
                ante: smallBlind,
                bringIn: bigBlind / 2,
                smallBet: bigBlind,
                bigBet: bigBlind * 2,
                useBringIn: true));
    }

    private void RegisterFiveCardDraw()
    {
        var info = new GameVariantInfo(
            Id: "five-card-draw",
            Name: "Five Card Draw",
            Description: "A classic draw poker variant. Each player receives 5 cards, then may discard up to 3 cards and draw new ones. Uses antes instead of blinds.",
            MinPlayers: 2,
            MaxPlayers: 6);

        // Map smallBlind to ante and bigBlind to minBet
        _registry.RegisterVariant(info, (players, smallBlind, bigBlind) =>
            new FiveCardDrawGame(
                players,
                ante: smallBlind,
                minBet: bigBlind));
    }

    private void RegisterFollowTheQueen()
    {
        var info = new GameVariantInfo(
            Id: "follow-the-queen",
            Name: "Follow the Queen",
            Description: "A seven card stud variant where Queens are always wild, and the card following the last dealt face-up Queen (and all cards of that rank) are also wild.",
            MinPlayers: 2,
            MaxPlayers: 7);

        // Map smallBlind to ante and bigBlind to smallBet
        // bringIn is set to half of smallBet, bigBet is double smallBet
        _registry.RegisterVariant(info, (players, smallBlind, bigBlind) =>
            new FollowTheQueenGame(
                players,
                ante: smallBlind,
                bringIn: bigBlind / 2,
                smallBet: bigBlind,
                bigBet: bigBlind * 2,
                useBringIn: true));
    }

    private void RegisterBaseball()
    {
        var info = new GameVariantInfo(
            Id: "baseball",
            Name: "Baseball",
            Description: "A seven card stud variant where 3s and 9s are wild. When a 4 is dealt face up, the player may pay a fixed price to receive an extra face-down card.",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Map smallBlind to ante and bigBlind to smallBet
        // bringIn is set to half of smallBet, bigBet is double smallBet
        // buyCardPrice is set equal to bigBet
        _registry.RegisterVariant(info, (players, smallBlind, bigBlind) =>
            new BaseballGame(
                players,
                ante: smallBlind,
                bringIn: bigBlind / 2,
                smallBet: bigBlind,
                bigBet: bigBlind * 2,
                buyCardPrice: bigBlind,
                useBringIn: true));
    }
}
