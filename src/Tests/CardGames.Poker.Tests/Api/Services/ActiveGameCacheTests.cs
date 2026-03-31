#nullable enable

using System;
using System.Collections.Generic;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services.Cache;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class ActiveGameCacheTests
{
    private readonly ActiveGameCache _sut;

    public ActiveGameCacheTests()
    {
        _sut = new ActiveGameCache(Substitute.For<ILogger<ActiveGameCache>>());
    }

    [Fact]
    public void Set_StoresSnapshot_TryGetReturnsIt()
    {
        var gameId = Guid.NewGuid();
        var snapshot = CreateSnapshot(gameId, handNumber: 1);

        _sut.Set(gameId, snapshot);

        _sut.TryGet(gameId, out var result).Should().BeTrue();
        result.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void TryGet_WhenNotSet_ReturnsFalse()
    {
        _sut.TryGet(Guid.NewGuid(), out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void Set_OverwritesPreviousSnapshot()
    {
        var gameId = Guid.NewGuid();
        var first = CreateSnapshot(gameId, handNumber: 1);
        var second = CreateSnapshot(gameId, handNumber: 2);

        _sut.Set(gameId, first);
        _sut.Set(gameId, second);

        _sut.TryGet(gameId, out var result).Should().BeTrue();
        result!.HandNumber.Should().Be(2);
    }

    [Fact]
    public void Evict_RemovesCachedSnapshot()
    {
        var gameId = Guid.NewGuid();
        _sut.Set(gameId, CreateSnapshot(gameId, handNumber: 1));

        _sut.Evict(gameId);

        _sut.TryGet(gameId, out _).Should().BeFalse();
    }

    [Fact]
    public void Evict_NonExistentKey_DoesNotThrow()
    {
        var act = () => _sut.Evict(Guid.NewGuid());
        act.Should().NotThrow();
    }

    [Fact]
    public void Count_ReflectsNumberOfCachedGames()
    {
        _sut.Count.Should().Be(0);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.Set(id1, CreateSnapshot(id1, 1));
        _sut.Set(id2, CreateSnapshot(id2, 1));

        _sut.Count.Should().Be(2);

        _sut.Evict(id1);
        _sut.Count.Should().Be(1);
    }

    [Fact]
    public void GetActiveGameIds_ReturnsAllCachedGameIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.Set(id1, CreateSnapshot(id1, 1));
        _sut.Set(id2, CreateSnapshot(id2, 1));

        var ids = _sut.GetActiveGameIds();
        ids.Should().HaveCount(2);
        ids.Should().Contain(id1);
        ids.Should().Contain(id2);
    }

    [Fact]
    public void Set_MultipleConcurrentGames_IsolatesState()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var snapshot1 = CreateSnapshot(id1, handNumber: 3);
        var snapshot2 = CreateSnapshot(id2, handNumber: 7);

        _sut.Set(id1, snapshot1);
        _sut.Set(id2, snapshot2);

        _sut.TryGet(id1, out var result1).Should().BeTrue();
        result1!.HandNumber.Should().Be(3);

        _sut.TryGet(id2, out var result2).Should().BeTrue();
        result2!.HandNumber.Should().Be(7);
    }

    [Fact]
    public void Evict_OnlyRemovesTargetGame()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.Set(id1, CreateSnapshot(id1, 1));
        _sut.Set(id2, CreateSnapshot(id2, 1));

        _sut.Evict(id1);

        _sut.TryGet(id1, out _).Should().BeFalse();
        _sut.TryGet(id2, out _).Should().BeTrue();
    }

    private static CachedGameSnapshot CreateSnapshot(Guid gameId, int handNumber)
    {
        return new CachedGameSnapshot
        {
            PublicState = new TableStatePublicDto
            {
                GameId = gameId,
                GameTypeCode = "TEST",
                CurrentPhase = "Betting",
                CurrentHandNumber = handNumber,
                CurrentActorSeatIndex = 0,
                Seats = Array.Empty<SeatPublicDto>()
            },
            PrivateStates = new Dictionary<string, PrivateStateDto>(),
            PlayerUserIds = Array.Empty<string>(),
            CapturedAt = DateTimeOffset.UtcNow,
            HandNumber = handNumber
        };
    }
}
