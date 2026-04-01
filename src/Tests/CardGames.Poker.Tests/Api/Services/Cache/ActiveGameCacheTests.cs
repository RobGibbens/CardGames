#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services.Cache;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.Cache;

public class ActiveGameCacheTests
{
    private readonly ActiveGameCache _sut;

    public ActiveGameCacheTests()
    {
        var options = Options.Create(new ActiveGameCacheOptions { Enabled = true });
        var logger = Substitute.For<ILogger<ActiveGameCache>>();
        var meterFactory = new TestMeterFactory();
        _sut = new ActiveGameCache(options, logger, meterFactory);
    }

    [Fact]
    public void TryGet_EmptyCache_ReturnsFalse()
    {
        _sut.TryGet(Guid.NewGuid(), out _).Should().BeFalse();
        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsStoredSnapshot()
    {
        var snapshot = CreateSnapshot(version: 1);

        _sut.Set(snapshot);

        _sut.TryGet(snapshot.GameId, out var retrieved).Should().BeTrue();
        retrieved.Should().BeSameAs(snapshot);
        _sut.Count.Should().Be(1);
    }

    [Fact]
    public void Set_RejectsStaleVersion()
    {
        var gameId = Guid.NewGuid();
        var newer = CreateSnapshot(gameId: gameId, version: 10);
        var older = CreateSnapshot(gameId: gameId, version: 5);

        _sut.Set(newer);
        _sut.Set(older);

        _sut.TryGet(gameId, out var retrieved).Should().BeTrue();
        retrieved.VersionNumber.Should().Be(10);
    }

    [Fact]
    public void Set_RejectsEqualVersion()
    {
        var gameId = Guid.NewGuid();
        var first = CreateSnapshot(gameId: gameId, version: 10, phase: "Dealing");
        var second = CreateSnapshot(gameId: gameId, version: 10, phase: "Betting");

        _sut.Set(first);
        _sut.Set(second);

        _sut.TryGet(gameId, out var retrieved).Should().BeTrue();
        retrieved.Phase.Should().Be("Dealing");
    }

    [Fact]
    public void Set_AcceptsNewerVersion()
    {
        var gameId = Guid.NewGuid();
        var older = CreateSnapshot(gameId: gameId, version: 5, phase: "Dealing");
        var newer = CreateSnapshot(gameId: gameId, version: 10, phase: "Betting");

        _sut.Set(older);
        _sut.Set(newer);

        _sut.TryGet(gameId, out var retrieved).Should().BeTrue();
        retrieved.VersionNumber.Should().Be(10);
        retrieved.Phase.Should().Be("Betting");
    }

    [Fact]
    public void Evict_RemovesExistingEntry()
    {
        var snapshot = CreateSnapshot(version: 1);
        _sut.Set(snapshot);

        _sut.Evict(snapshot.GameId).Should().BeTrue();

        _sut.TryGet(snapshot.GameId, out _).Should().BeFalse();
        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void Evict_ReturnsFalseForMissingEntry()
    {
        _sut.Evict(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Compact_RemovesOldEntries()
    {
        var old = CreateSnapshot(version: 1, builtAt: DateTimeOffset.UtcNow.AddHours(-2));
        var recent = CreateSnapshot(version: 1, builtAt: DateTimeOffset.UtcNow);

        _sut.Set(old);
        _sut.Set(recent);

        var evicted = _sut.Compact(DateTimeOffset.UtcNow.AddHours(-1));

        evicted.Should().Be(1);
        _sut.Count.Should().Be(1);
        _sut.TryGet(recent.GameId, out _).Should().BeTrue();
    }

    [Fact]
    public void GetActiveGameIds_ReturnsAllCachedIds()
    {
        var s1 = CreateSnapshot(version: 1);
        var s2 = CreateSnapshot(version: 1);

        _sut.Set(s1);
        _sut.Set(s2);

        _sut.GetActiveGameIds().Should().BeEquivalentTo([s1.GameId, s2.GameId]);
    }

    [Fact]
    public void UpsertPrivateState_AddsToExistingSnapshot()
    {
        var gameId = Guid.NewGuid();
        var snapshot = CreateSnapshot(gameId: gameId, version: 5);
        _sut.Set(snapshot);

        var newPrivate = new PrivateStateDto
        {
            GameId = gameId,
            PlayerName = "NewPlayer",
            Hand = Array.Empty<CardPrivateDto>()
        };

        _sut.UpsertPrivateState(gameId, "newuser@test.com", newPrivate, 5);

        _sut.TryGet(gameId, out var updated).Should().BeTrue();
        updated.PrivateStatesByUserId.Should().ContainKey("newuser@test.com");
        updated.PrivateStatesByUserId["newuser@test.com"].Should().BeSameAs(newPrivate);
    }

    [Fact]
    public void UpsertPrivateState_RejectsOlderVersion()
    {
        var gameId = Guid.NewGuid();
        var snapshot = CreateSnapshot(gameId: gameId, version: 10);
        _sut.Set(snapshot);

        var newPrivate = new PrivateStateDto
        {
            GameId = gameId,
            PlayerName = "StalePlayer",
            Hand = Array.Empty<CardPrivateDto>()
        };

        _sut.UpsertPrivateState(gameId, "stale@test.com", newPrivate, 5);

        _sut.TryGet(gameId, out var result).Should().BeTrue();
        result.PrivateStatesByUserId.Should().NotContainKey("stale@test.com");
    }

    [Fact]
    public void UpsertPrivateState_NoopWhenNoExistingSnapshot()
    {
        var newPrivate = new PrivateStateDto
        {
            GameId = Guid.NewGuid(),
            PlayerName = "Ghost",
            Hand = Array.Empty<CardPrivateDto>()
        };

        _sut.UpsertPrivateState(Guid.NewGuid(), "ghost@test.com", newPrivate, 1);

        _sut.Count.Should().Be(0);
    }

    [Fact]
    public void IsolatesPrivateStatesByUserId()
    {
        var gameId = Guid.NewGuid();
        var privateStates = ImmutableDictionary.CreateBuilder<string, PrivateStateDto>(StringComparer.OrdinalIgnoreCase);
        privateStates["alice@test.com"] = new PrivateStateDto
        {
            GameId = gameId, PlayerName = "Alice", Hand = Array.Empty<CardPrivateDto>()
        };
        privateStates["bob@test.com"] = new PrivateStateDto
        {
            GameId = gameId, PlayerName = "Bob", Hand = Array.Empty<CardPrivateDto>()
        };

        var snapshot = new CachedGameSnapshot
        {
            GameId = gameId,
            VersionNumber = 1,
            PublicState = CreatePublicState(gameId),
            PrivateStatesByUserId = privateStates.ToImmutable(),
            PlayerUserIds = ["alice@test.com", "bob@test.com"],
            HandNumber = 1,
            Phase = "Dealing",
            BuiltAtUtc = DateTimeOffset.UtcNow
        };

        _sut.Set(snapshot);
        _sut.TryGet(gameId, out var cached).Should().BeTrue();

        cached.PrivateStatesByUserId["alice@test.com"].PlayerName.Should().Be("Alice");
        cached.PrivateStatesByUserId["bob@test.com"].PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void Disabled_SetAndTryGetAreNoOps()
    {
        var options = Options.Create(new ActiveGameCacheOptions { Enabled = false });
        var cache = new ActiveGameCache(options, Substitute.For<ILogger<ActiveGameCache>>(), new TestMeterFactory());
        var snapshot = CreateSnapshot(version: 1);

        cache.Set(snapshot);

        cache.TryGet(snapshot.GameId, out _).Should().BeFalse();
        cache.Count.Should().Be(0);
    }

    private static CachedGameSnapshot CreateSnapshot(
        Guid? gameId = null,
        ulong version = 1,
        string phase = "Dealing",
        DateTimeOffset? builtAt = null) => new()
    {
        GameId = gameId ?? Guid.NewGuid(),
        VersionNumber = version,
        PublicState = CreatePublicState(gameId ?? Guid.NewGuid()),
        PrivateStatesByUserId = ImmutableDictionary<string, PrivateStateDto>.Empty,
        PlayerUserIds = ImmutableArray<string>.Empty,
        HandNumber = 1,
        Phase = phase,
        BuiltAtUtc = builtAt ?? DateTimeOffset.UtcNow
    };

    private static TableStatePublicDto CreatePublicState(Guid gameId) => new()
    {
        GameId = gameId,
        CurrentPhase = "Dealing",
        Seats = Array.Empty<SeatPublicDto>()
    };

    /// <summary>
    /// Minimal IMeterFactory for unit tests.
    /// </summary>
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
