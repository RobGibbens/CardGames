using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Background service that periodically checkpoints all dirty in-memory game states
/// to the database. Ensures that in-flight game state is persisted regularly to
/// limit data loss in the event of an unplanned shutdown.
/// </summary>
public sealed class GameStatePeriodicCheckpointService : BackgroundService
{
    private readonly IGameStateManager _gameStateManager;
    private readonly IGameStateCheckpointService _checkpointService;
    private readonly IOptions<InMemoryEngineOptions> _options;
    private readonly ILogger<GameStatePeriodicCheckpointService> _logger;

    public GameStatePeriodicCheckpointService(
        IGameStateManager gameStateManager,
        IGameStateCheckpointService checkpointService,
        IOptions<InMemoryEngineOptions> options,
        ILogger<GameStatePeriodicCheckpointService> logger)
    {
        _gameStateManager = gameStateManager;
        _checkpointService = checkpointService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("In-memory engine is disabled — periodic checkpoint service will not run");
            return;
        }

        _logger.LogInformation(
            "Periodic checkpoint service started (interval: {Interval})",
            _options.Value.CheckpointInterval);

        using var timer = new PeriodicTimer(_options.Value.CheckpointInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckpointDirtyGamesAsync(stoppingToken);
        }
    }

    private async Task CheckpointDirtyGamesAsync(CancellationToken cancellationToken)
    {
        var gameIds = _gameStateManager.GetActiveGameIds();
        var checkpointed = 0;

        foreach (var gameId in gameIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!_gameStateManager.TryGetGame(gameId, out var state) || !state.IsDirty)
                continue;

            try
            {
                await _checkpointService.CheckpointAsync(state, cancellationToken);
                checkpointed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to checkpoint game {GameId}", gameId);
            }
        }

        if (checkpointed > 0)
        {
            _logger.LogDebug("Periodic checkpoint completed — {Count} game(s) flushed", checkpointed);
        }
    }
}
