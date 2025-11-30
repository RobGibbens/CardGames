using System.Collections.Generic;
using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class BlindPostingServiceTests
{
    private readonly BlindPostingService _service = new();

    #region PostBlinds Tests

    [Fact]
    public void PostBlinds_TwoPlayers_PostsSmallAndBigBlinds()
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", 1000),
            new("Bob", 1000)
        };
        var potManager = new PotManager();

        var result = _service.PostBlinds(players, 0, 1, 5, 10, potManager);

        result.Success.Should().BeTrue();
        result.PostedBlinds.Should().HaveCount(2);
        result.TotalCollected.Should().Be(15);
        
        var sbBlind = result.PostedBlinds[0];
        sbBlind.Type.Should().Be(BlindType.SmallBlind);
        sbBlind.Amount.Should().Be(5);
        sbBlind.PlayerName.Should().Be("Alice");

        var bbBlind = result.PostedBlinds[1];
        bbBlind.Type.Should().Be(BlindType.BigBlind);
        bbBlind.Amount.Should().Be(10);
        bbBlind.PlayerName.Should().Be("Bob");
    }

    [Fact]
    public void PostBlinds_SmallStackSmallBlind_PostsAllIn()
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", 3), // Only 3 chips
            new("Bob", 1000)
        };
        var potManager = new PotManager();

        var result = _service.PostBlinds(players, 0, 1, 5, 10, potManager);

        result.Success.Should().BeTrue();
        result.PostedBlinds[0].Amount.Should().Be(3); // All-in for 3
        result.PostedBlinds[1].Amount.Should().Be(10);
        result.TotalCollected.Should().Be(13);
    }

    [Fact]
    public void PostBlinds_InsufficientPlayers_ReturnsFalse()
    {
        var players = new List<PokerPlayer> { new("Alice", 1000) };
        var potManager = new PotManager();

        var result = _service.PostBlinds(players, 0, 0, 5, 10, potManager);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least 2");
    }

    #endregion

    #region CollectAntes Tests

    [Fact]
    public void CollectAntes_ThreePlayers_CollectsFromAll()
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", 1000),
            new("Bob", 1000),
            new("Charlie", 1000)
        };
        var potManager = new PotManager();

        var result = _service.CollectAntes(players, 5, potManager);

        result.Success.Should().BeTrue();
        result.PostedAntes.Should().HaveCount(3);
        result.TotalCollected.Should().Be(15);
        result.PostedAntes.Should().AllSatisfy(a => a.Type.Should().Be(BlindType.Ante));
    }

    [Fact]
    public void CollectAntes_ZeroAnte_ReturnsEmpty()
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", 1000),
            new("Bob", 1000)
        };
        var potManager = new PotManager();

        var result = _service.CollectAntes(players, 0, potManager);

        result.Success.Should().BeTrue();
        result.PostedAntes.Should().BeEmpty();
        result.TotalCollected.Should().Be(0);
    }

    [Fact]
    public void CollectAntes_ShortStack_PostsAllIn()
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", 1000),
            new("Bob", 2) // Short stack
        };
        var potManager = new PotManager();

        var result = _service.CollectAntes(players, 5, potManager);

        result.Success.Should().BeTrue();
        result.PostedAntes.Should().HaveCount(2);
        result.PostedAntes[0].Amount.Should().Be(5);
        result.PostedAntes[1].Amount.Should().Be(2);
        result.TotalCollected.Should().Be(7);
    }

    [Fact]
    public void CollectAntes_ResetsCurrentBets()
    {
        var players = new List<PokerPlayer>
        {
            new("Alice", 1000),
            new("Bob", 1000)
        };
        var potManager = new PotManager();

        _service.CollectAntes(players, 5, potManager);

        // Antes should not count as current bet
        players[0].CurrentBet.Should().Be(0);
        players[1].CurrentBet.Should().Be(0);
    }

    #endregion

    #region MissedBlinds Tests

    [Fact]
    public void RecordMissedBlinds_RecordsBothBlinds()
    {
        _service.RecordMissedBlinds("Alice", true, true, 5, 10, 10);

        var info = _service.GetMissedBlinds("Alice");
        
        info.Should().NotBeNull();
        info!.MissedSmallBlind.Should().BeTrue();
        info.MissedBigBlind.Should().BeTrue();
        info.TotalMissedAmount.Should().Be(15);
        info.HandNumberMissed.Should().Be(10);
    }

    [Fact]
    public void RecordMissedBlinds_OnlySmallBlind()
    {
        _service.RecordMissedBlinds("Bob", true, false, 5, 10, 5);

        var info = _service.GetMissedBlinds("Bob");
        
        info!.MissedSmallBlind.Should().BeTrue();
        info.MissedBigBlind.Should().BeFalse();
        info.TotalMissedAmount.Should().Be(5);
    }

    [Fact]
    public void HasMissedBlinds_ReturnsTrueWhenRecorded()
    {
        _service.RecordMissedBlinds("Alice", true, true, 5, 10, 1);

        _service.HasMissedBlinds("Alice").Should().BeTrue();
        _service.HasMissedBlinds("Bob").Should().BeFalse();
    }

    [Fact]
    public void PostMissedBlinds_PostsBothBlinds()
    {
        var player = new PokerPlayer("Alice", 1000);
        var potManager = new PotManager();
        _service.RecordMissedBlinds("Alice", true, true, 5, 10, 1);

        var postedBlinds = _service.PostMissedBlinds(player, 0, 10, potManager);

        postedBlinds.Should().HaveCount(2);
        
        // Small blind should be posted as dead
        var deadBlind = postedBlinds[0];
        deadBlind.Type.Should().Be(BlindType.MissedSmallBlind);
        deadBlind.IsDead.Should().BeTrue();
        deadBlind.Amount.Should().Be(5);

        // Big blind should be posted as live
        var liveBlind = postedBlinds[1];
        liveBlind.Type.Should().Be(BlindType.MissedBigBlind);
        liveBlind.IsDead.Should().BeFalse();
        liveBlind.Amount.Should().Be(10);
    }

    [Fact]
    public void PostMissedBlinds_ClearsRecord()
    {
        var player = new PokerPlayer("Alice", 1000);
        var potManager = new PotManager();
        _service.RecordMissedBlinds("Alice", true, true, 5, 10, 1);

        _service.PostMissedBlinds(player, 0, 10, potManager);

        _service.HasMissedBlinds("Alice").Should().BeFalse();
    }

    [Fact]
    public void PostMissedBlinds_NoMissedBlinds_ReturnsEmpty()
    {
        var player = new PokerPlayer("Alice", 1000);
        var potManager = new PotManager();

        var postedBlinds = _service.PostMissedBlinds(player, 0, 10, potManager);

        postedBlinds.Should().BeEmpty();
    }

    [Fact]
    public void ClearAllMissedBlinds_ClearsAll()
    {
        _service.RecordMissedBlinds("Alice", true, true, 5, 10, 1);
        _service.RecordMissedBlinds("Bob", false, true, 5, 10, 2);

        _service.ClearAllMissedBlinds();

        _service.HasMissedBlinds("Alice").Should().BeFalse();
        _service.HasMissedBlinds("Bob").Should().BeFalse();
    }

    [Fact]
    public void GetAllMissedBlinds_ReturnsAllRecords()
    {
        _service.RecordMissedBlinds("Alice", true, true, 5, 10, 1);
        _service.RecordMissedBlinds("Bob", false, true, 5, 10, 2);

        var allMissed = _service.GetAllMissedBlinds();

        allMissed.Should().HaveCount(2);
        allMissed.Should().ContainKey("Alice");
        allMissed.Should().ContainKey("Bob");
    }

    #endregion
}
