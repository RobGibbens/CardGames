using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// On application startup, recovers active games from the database into the
/// in-memory game state manager so that play can resume without waiting for
/// the first request to trigger lazy hydration.
/// </summary>
public sealed class GameStateRecoveryService : BackgroundService
{
    private readonly IGameStateManager _gameStateManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<InMemoryEngineOptions> _options;
    private readonly ILogger<GameStateRecoveryService> _logger;

    public GameStateRecoveryService(
        IGameStateManager gameStateManager,
        IServiceScopeFactory scopeFactory,
        IOptions<InMemoryEngineOptions> options,
        ILogger<GameStateRecoveryService> logger)
    {
        _gameStateManager = gameStateManager;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("In-memory engine is disabled — startup recovery will not run");
            return;
        }

        _logger.LogInformation("Starting in-memory engine recovery — loading active games from database");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CardsDbContext>();

            var activeGameIds = await db.Games
                .Where(g => g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands)
                .Select(g => g.Id)
                .ToListAsync(stoppingToken);

            var recovered = 0;
            foreach (var gameId in activeGameIds)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var state = await _gameStateManager.GetOrLoadGameAsync(gameId, stoppingToken);
                    if (state is not null)
                        recovered++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to recover game {GameId}", gameId);
                }
            }

            _logger.LogInformation(
                "In-memory engine recovery complete — {Recovered}/{Total} active game(s) loaded",
                recovered, activeGameIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "In-memory engine recovery failed");
        }
    }
}
