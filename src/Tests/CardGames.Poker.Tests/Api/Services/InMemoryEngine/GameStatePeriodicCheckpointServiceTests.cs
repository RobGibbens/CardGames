using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.InMemoryEngine;

public class GameStatePeriodicCheckpointServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Disabled_DoesNotCheckpoint()
    {
        var gameStateManager = Substitute.For<IGameStateManager>();
        var checkpointService = Substitute.For<IGameStateCheckpointService>();
        var options = Options.Create(new InMemoryEngineOptions { Enabled = false });
        var logger = Substitute.For<ILogger<GameStatePeriodicCheckpointService>>();

        var sut = new GameStatePeriodicCheckpointService(gameStateManager, checkpointService, options, logger);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await sut.StopAsync(CancellationToken.None);

        gameStateManager.DidNotReceive().GetActiveGameIds();
        await checkpointService.DidNotReceive()
            .CheckpointAsync(Arg.Any<ActiveGameRuntimeState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Enabled_CheckpointsDirtyGames()
    {
        var dirtyGame = new ActiveGameRuntimeState { GameId = Guid.NewGuid(), IsDirty = true };
        var cleanGame = new ActiveGameRuntimeState { GameId = Guid.NewGuid(), IsDirty = false };

        var gameStateManager = Substitute.For<IGameStateManager>();
        gameStateManager.GetActiveGameIds().Returns([dirtyGame.GameId, cleanGame.GameId]);
        gameStateManager.TryGetGame(dirtyGame.GameId, out Arg.Any<ActiveGameRuntimeState>())
            .Returns(x => { x[1] = dirtyGame; return true; });
        gameStateManager.TryGetGame(cleanGame.GameId, out Arg.Any<ActiveGameRuntimeState>())
            .Returns(x => { x[1] = cleanGame; return true; });

        var checkpointService = Substitute.For<IGameStateCheckpointService>();
        var options = Options.Create(new InMemoryEngineOptions
        {
            Enabled = true,
            CheckpointInterval = TimeSpan.FromMilliseconds(50),
        });
        var logger = Substitute.For<ILogger<GameStatePeriodicCheckpointService>>();

        var sut = new GameStatePeriodicCheckpointService(gameStateManager, checkpointService, options, logger);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await sut.StartAsync(cts.Token);
        await Task.Delay(150);
        await sut.StopAsync(CancellationToken.None);

        // Only the dirty game should be checkpointed
        await checkpointService.Received().CheckpointAsync(dirtyGame, Arg.Any<CancellationToken>());
        await checkpointService.DidNotReceive().CheckpointAsync(cleanGame, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CheckpointFailure_ContinuesWithOtherGames()
    {
        var game1 = new ActiveGameRuntimeState { GameId = Guid.NewGuid(), IsDirty = true };
        var game2 = new ActiveGameRuntimeState { GameId = Guid.NewGuid(), IsDirty = true };

        var gameStateManager = Substitute.For<IGameStateManager>();
        gameStateManager.GetActiveGameIds().Returns([game1.GameId, game2.GameId]);
        gameStateManager.TryGetGame(game1.GameId, out Arg.Any<ActiveGameRuntimeState>())
            .Returns(x => { x[1] = game1; return true; });
        gameStateManager.TryGetGame(game2.GameId, out Arg.Any<ActiveGameRuntimeState>())
            .Returns(x => { x[1] = game2; return true; });

        var checkpointService = Substitute.For<IGameStateCheckpointService>();
        checkpointService.CheckpointAsync(game1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var options = Options.Create(new InMemoryEngineOptions
        {
            Enabled = true,
            CheckpointInterval = TimeSpan.FromMilliseconds(50),
        });
        var logger = Substitute.For<ILogger<GameStatePeriodicCheckpointService>>();

        var sut = new GameStatePeriodicCheckpointService(gameStateManager, checkpointService, options, logger);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await sut.StartAsync(cts.Token);
        await Task.Delay(150);
        await sut.StopAsync(CancellationToken.None);

        // game2 should still be checkpointed even though game1 failed
        await checkpointService.Received().CheckpointAsync(game2, Arg.Any<CancellationToken>());
    }
}
