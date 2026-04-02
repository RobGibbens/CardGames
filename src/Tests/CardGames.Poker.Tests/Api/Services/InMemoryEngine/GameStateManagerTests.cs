using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.InMemoryEngine;

public class GameStateManagerTests
{
    private readonly IGameStateHydrator _hydrator;
    private readonly GameStateManager _sut;

    public GameStateManagerTests()
    {
        _hydrator = Substitute.For<IGameStateHydrator>();
        var options = Options.Create(new InMemoryEngineOptions { Enabled = true });
        var logger = Substitute.For<ILogger<GameStateManager>>();
        var meterFactory = new TestMeterFactory();
        _sut = new GameStateManager(_hydrator, options, logger, meterFactory);
    }

    [Fact]
    public void TryGetGame_EmptyManager_ReturnsFalse()
    {
        _sut.TryGetGame(Guid.NewGuid(), out _).Should().BeFalse();
    }

    [Fact]
    public void Count_EmptyManager_ReturnsZero()
    {
        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void SetGame_ThenTryGet_ReturnsTrue()
    {
        var state = new ActiveGameRuntimeState { GameId = Guid.NewGuid() };

        _sut.SetGame(state);

        _sut.TryGetGame(state.GameId, out var result).Should().BeTrue();
        result.Should().BeSameAs(state);
        _sut.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetOrLoadGameAsync_CacheHit_ReturnsExisting()
    {
        var state = new ActiveGameRuntimeState { GameId = Guid.NewGuid() };
        _sut.SetGame(state);

        var result = await _sut.GetOrLoadGameAsync(state.GameId, CancellationToken.None);

        result.Should().BeSameAs(state);
        await _hydrator.DidNotReceive().HydrateFromDatabaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrLoadGameAsync_CacheMiss_HydratesFromDatabase()
    {
        var gameId = Guid.NewGuid();
        var hydratedState = new ActiveGameRuntimeState { GameId = gameId, CurrentPhase = "Dealing" };
        _hydrator.HydrateFromDatabaseAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(hydratedState);

        var result = await _sut.GetOrLoadGameAsync(gameId, CancellationToken.None);

        result.Should().BeSameAs(hydratedState);
        _sut.TryGetGame(gameId, out _).Should().BeTrue();
        _sut.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetOrLoadGameAsync_GameNotInDb_ReturnsNull()
    {
        var gameId = Guid.NewGuid();
        _hydrator.HydrateFromDatabaseAsync(gameId, Arg.Any<CancellationToken>())
            .Returns((ActiveGameRuntimeState?)null);

        var result = await _sut.GetOrLoadGameAsync(gameId, CancellationToken.None);

        result.Should().BeNull();
        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveGame_ExistingGame_ReturnsTrueAndRemoves()
    {
        var state = new ActiveGameRuntimeState { GameId = Guid.NewGuid() };
        _sut.SetGame(state);

        _sut.RemoveGame(state.GameId).Should().BeTrue();
        _sut.TryGetGame(state.GameId, out _).Should().BeFalse();
        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveGame_NonExistentGame_ReturnsFalse()
    {
        _sut.RemoveGame(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void GetActiveGameIds_ReturnsAllGameIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.SetGame(new ActiveGameRuntimeState { GameId = id1 });
        _sut.SetGame(new ActiveGameRuntimeState { GameId = id2 });

        var ids = _sut.GetActiveGameIds();

        ids.Should().HaveCount(2);
        ids.Should().Contain(id1);
        ids.Should().Contain(id2);
    }

    [Fact]
    public void SetGame_OverwritesExisting()
    {
        var gameId = Guid.NewGuid();
        var state1 = new ActiveGameRuntimeState { GameId = gameId, CurrentPhase = "Dealing" };
        var state2 = new ActiveGameRuntimeState { GameId = gameId, CurrentPhase = "Betting" };

        _sut.SetGame(state1);
        _sut.SetGame(state2);

        _sut.TryGetGame(gameId, out var result).Should().BeTrue();
        result!.CurrentPhase.Should().Be("Betting");
        _sut.Count.Should().Be(1);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var m in _meters) m.Dispose();
        }
    }
}
