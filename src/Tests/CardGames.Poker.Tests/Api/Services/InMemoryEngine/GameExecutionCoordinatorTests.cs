using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.InMemoryEngine;

public class GameExecutionCoordinatorTests : IDisposable
{
    private readonly IGameStateManager _stateManager;
    private readonly GameExecutionCoordinator _sut;

    public GameExecutionCoordinatorTests()
    {
        _stateManager = Substitute.For<IGameStateManager>();
        var logger = Substitute.For<ILogger<GameExecutionCoordinator>>();
        _sut = new GameExecutionCoordinator(_stateManager, logger);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task ExecuteAsync_LoadsGameAndReturnsResult()
    {
        var gameId = Guid.NewGuid();
        var state = new ActiveGameRuntimeState { GameId = gameId, CurrentPhase = "Dealing" };
        _stateManager.GetOrLoadGameAsync(gameId, Arg.Any<CancellationToken>()).Returns(state);

        var result = await _sut.ExecuteAsync(gameId, (s, _) =>
        {
            s.CurrentPhase = "Betting";
            return Task.FromResult(42);
        }, CancellationToken.None);

        result.Should().Be(42);
        state.CurrentPhase.Should().Be("Betting");
    }

    [Fact]
    public async Task ExecuteAsync_SetsIsDirtyAndIncrementsVersion()
    {
        var gameId = Guid.NewGuid();
        var state = new ActiveGameRuntimeState { GameId = gameId, Version = 5, IsDirty = false };
        _stateManager.GetOrLoadGameAsync(gameId, Arg.Any<CancellationToken>()).Returns(state);

        await _sut.ExecuteAsync(gameId, (_, _) => Task.FromResult(0), CancellationToken.None);

        state.IsDirty.Should().BeTrue();
        state.Version.Should().Be(6);
    }

    [Fact]
    public async Task ExecuteAsync_GameNotFound_ThrowsInvalidOperationException()
    {
        var gameId = Guid.NewGuid();
        _stateManager.GetOrLoadGameAsync(gameId, Arg.Any<CancellationToken>())
            .Returns((ActiveGameRuntimeState?)null);

        var act = () => _sut.ExecuteAsync(gameId, (_, _) => Task.FromResult(0), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Game {gameId} not found*");
    }

    [Fact]
    public async Task ExecuteAsync_SerializesAccessPerGame()
    {
        var gameId = Guid.NewGuid();
        var state = new ActiveGameRuntimeState { GameId = gameId };
        _stateManager.GetOrLoadGameAsync(gameId, Arg.Any<CancellationToken>()).Returns(state);

        var entryCount = 0;
        var maxConcurrent = 0;

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _sut.ExecuteAsync(gameId, async (_, _) =>
            {
                var current = Interlocked.Increment(ref entryCount);
                var localMax = Interlocked.CompareExchange(ref maxConcurrent, 0, 0);
                if (current > localMax)
                    Interlocked.CompareExchange(ref maxConcurrent, current, localMax);

                await Task.Delay(10);
                Interlocked.Decrement(ref entryCount);
                return 0;
            }, CancellationToken.None));

        await Task.WhenAll(tasks);

        maxConcurrent.Should().Be(1, "only one action should execute per game at a time");
    }

    [Fact]
    public async Task ExecuteAsync_DifferentGames_RunConcurrently()
    {
        var game1 = Guid.NewGuid();
        var game2 = Guid.NewGuid();
        var state1 = new ActiveGameRuntimeState { GameId = game1 };
        var state2 = new ActiveGameRuntimeState { GameId = game2 };
        _stateManager.GetOrLoadGameAsync(game1, Arg.Any<CancellationToken>()).Returns(state1);
        _stateManager.GetOrLoadGameAsync(game2, Arg.Any<CancellationToken>()).Returns(state2);

        var concurrentCount = 0;
        var maxConcurrent = 0;
        var barrier = new TaskCompletionSource();

        var t1 = _sut.ExecuteAsync(game1, async (_, _) =>
        {
            var c = Interlocked.Increment(ref concurrentCount);
            if (c > Interlocked.CompareExchange(ref maxConcurrent, 0, 0))
                Interlocked.Exchange(ref maxConcurrent, c);
            barrier.TrySetResult();
            await Task.Delay(50);
            Interlocked.Decrement(ref concurrentCount);
            return 0;
        }, CancellationToken.None);

        var t2 = _sut.ExecuteAsync(game2, async (_, _) =>
        {
            await barrier.Task;
            var c = Interlocked.Increment(ref concurrentCount);
            if (c > Interlocked.CompareExchange(ref maxConcurrent, 0, 0))
                Interlocked.Exchange(ref maxConcurrent, c);
            await Task.Delay(50);
            Interlocked.Decrement(ref concurrentCount);
            return 0;
        }, CancellationToken.None);

        await Task.WhenAll(t1, t2);

        maxConcurrent.Should().BeGreaterThanOrEqualTo(2, "different games should execute concurrently");
    }

    [Fact]
    public async Task ExecuteAsync_VoidOverload_Works()
    {
        var gameId = Guid.NewGuid();
        var state = new ActiveGameRuntimeState { GameId = gameId, CurrentPhase = "Dealing" };
        _stateManager.GetOrLoadGameAsync(gameId, Arg.Any<CancellationToken>()).Returns(state);

        await _sut.ExecuteAsync(gameId, (s, _) =>
        {
            s.CurrentPhase = "Complete";
            return Task.CompletedTask;
        }, CancellationToken.None);

        state.CurrentPhase.Should().Be("Complete");
        state.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void CleanupLock_RemovesSemaphore()
    {
        var gameId = Guid.NewGuid();
        // Create a lock by triggering state load (even though it will fail, semaphore is created)
        _stateManager.GetOrLoadGameAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new ActiveGameRuntimeState { GameId = gameId });

        // Execute to create the lock entry
        _sut.ExecuteAsync(gameId, (_, _) => Task.FromResult(0), CancellationToken.None).GetAwaiter().GetResult();

        // Cleanup should not throw
        _sut.CleanupLock(gameId);

        // Cleanup of non-existent lock should also not throw
        _sut.CleanupLock(Guid.NewGuid());
    }
}
