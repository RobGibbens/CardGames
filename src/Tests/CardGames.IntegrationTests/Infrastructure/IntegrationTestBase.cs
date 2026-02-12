using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Infrastructure;
using CardGames.IntegrationTests.Infrastructure.Fakes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CardGames.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that require database access and DI services.
/// Uses an in-memory database for fast, isolated testing of handlers and game flows.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected IServiceScope Scope { get; private set; } = null!;
    protected CardsDbContext DbContext => Scope.ServiceProvider.GetRequiredService<CardsDbContext>();
    protected IMediator Mediator => Scope.ServiceProvider.GetRequiredService<IMediator>();
    protected IGameFlowHandlerFactory FlowHandlerFactory => Scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();
    
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid():N}";
    private readonly InMemoryDatabaseRoot _databaseRoot = new();

    public virtual async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));

        // Add in-memory database
        services.AddDbContext<CardsDbContext>(options =>
            options.UseInMemoryDatabase(_databaseName, _databaseRoot));

        // Add MediatR with handler registrations from the API project
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(CardsDbContext).Assembly);
        });

        // Add game flow handler factory
        services.AddSingleton<IGameFlowHandlerFactory, GameFlowHandlerFactory>();

        // Register HybridCache for tests
#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        // Add services
        services.AddSingleton<IActionTimerService, FakeActionTimerService>();
        services.AddSingleton<IGameStateBroadcaster, FakeGameStateBroadcaster>();
        services.AddSingleton<IHandHistoryRecorder, FakeHandHistoryRecorder>();
        services.AddScoped<ICurrentUserService, FakeCurrentUserService>();
        services.AddScoped<ITableStateBuilder, TableStateBuilder>();

        ServiceProvider = services.BuildServiceProvider();
        Scope = ServiceProvider.CreateScope();

        // Ensure database is created and seed base data
        await DbContext.Database.EnsureCreatedAsync();
        await SeedBaseDataAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        Scope.Dispose();
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Seeds base data required for tests (game types, etc.)
    /// </summary>
    protected virtual async Task SeedBaseDataAsync()
    {
        // Seed game types
        var gameTypes = new List<GameType>
        {
            CreateGameType("FIVECARDDRAW", "Five Card Draw", 2, 8, 5, 0, 0, 5),
            CreateGameType("SEVENCARDSTUD", "Seven Card Stud", 2, 8, 2, 1, 0, 7),
            CreateGameType("KINGSANDLOWS", "Kings and Lows", 2, 8, 5, 0, 0, 5),
            CreateGameType("TWOSJACKSMANWITHTHEAXE", "Twos, Jacks, and Man with the Axe", 2, 8, 5, 0, 0, 5),
            CreateGameType("HOLDEM", "Texas Hold'em", 2, 10, 2, 0, 5, 7),
            CreateGameType("OMAHA", "Omaha", 2, 10, 4, 0, 5, 9),
            CreateGameType("FOLLOWTHEQUEEN", "Follow the Queen", 2, 8, 2, 1, 0, 7),
            CreateGameType("BASEBALL", "Baseball", 2, 8, 2, 1, 0, 7)
        };

        await DbContext.GameTypes.AddRangeAsync(gameTypes);
        await DbContext.SaveChangesAsync();
    }

    private static GameType CreateGameType(
        string code, string name, int minPlayers, int maxPlayers,
        int holeCards, int boardCards, int communityCards, int maxPlayerCards)
    {
        return new GameType
        {
            Id = Guid.CreateVersion7(),
            Code = code,
            Name = name,
            MinPlayers = minPlayers,
            MaxPlayers = maxPlayers,
            InitialHoleCards = holeCards,
            InitialBoardCards = boardCards,
            MaxCommunityCards = communityCards,
            MaxPlayerCards = maxPlayerCards,
            BettingStructure = BettingStructure.Ante
        };
    }

    /// <summary>
    /// Creates a new scope for a fresh DbContext instance (for testing concurrent operations).
    /// </summary>
    protected IServiceScope CreateNewScope() => ServiceProvider.CreateScope();

    /// <summary>
    /// Gets a fresh DbContext from a new scope.
    /// </summary>
    protected CardsDbContext GetFreshDbContext()
    {
        var scope = CreateNewScope();
        return scope.ServiceProvider.GetRequiredService<CardsDbContext>();
    }
}
