using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.InMemoryEngine;

public class ActiveGameRuntimeStateTests
{
    private static ActiveGameRuntimeState CreateState(int handNumber = 1) => new()
    {
        GameId = Guid.NewGuid(),
        GameTypeCode = "HOLDEM",
        CurrentPhase = "Dealing",
        CurrentHandNumber = handNumber,
        Status = GameStatus.InProgress,
        Ante = 10,
        Players =
        [
            new RuntimeGamePlayer { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), SeatPosition = 0, Status = GamePlayerStatus.Active, ChipStack = 1000 },
            new RuntimeGamePlayer { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), SeatPosition = 1, Status = GamePlayerStatus.Active, ChipStack = 500 },
            new RuntimeGamePlayer { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), SeatPosition = 2, Status = GamePlayerStatus.Active, ChipStack = 200, IsSittingOut = true },
        ],
    };

    [Fact]
    public void NewState_IsDirty_IsFalse()
    {
        var state = new ActiveGameRuntimeState();
        state.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void NewState_Version_IsZero()
    {
        var state = new ActiveGameRuntimeState();
        state.Version.Should().Be(0);
    }

    [Fact]
    public void Setting_IsDirty_PersistsValue()
    {
        var state = new ActiveGameRuntimeState { IsDirty = true };
        state.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Collections_DefaultToEmpty()
    {
        var state = new ActiveGameRuntimeState();
        state.Players.Should().BeEmpty();
        state.Cards.Should().BeEmpty();
        state.Pots.Should().BeEmpty();
        state.BettingRounds.Should().BeEmpty();
    }

    [Fact]
    public void GetActivePlayersOrdered_ExcludesSittingOut()
    {
        var state = CreateState();
        // Player at seat 2 is Active but SittingOut — GetActivePlayersOrdered still returns
        // all Active-status players (sitting out is not filtered here, only by GetEligiblePlayers)
        state.GetActivePlayersOrdered().Should().HaveCount(3);
    }

    [Fact]
    public void GetEligiblePlayers_ExcludesSittingOutAndLowChips()
    {
        var state = CreateState();
        state.Ante = 10;
        state.Players[2].IsSittingOut = true; // sitting out

        var eligible = state.GetEligiblePlayers();

        eligible.Should().HaveCount(2);
        eligible.Should().OnlyContain(p => !p.IsSittingOut);
    }

    [Fact]
    public void GetEligiblePlayers_ExcludesPlayersWithInsufficientChips()
    {
        var state = CreateState();
        state.Ante = 600; // player at seat 1 has 500, can't play

        var eligible = state.GetEligiblePlayers();

        eligible.Should().HaveCount(1); // only seat 0 with 1000 (seat 2 is sitting out)
        eligible[0].SeatPosition.Should().Be(0);
    }

    [Fact]
    public void GetPlayersInHand_ExcludesFoldedPlayers()
    {
        var state = CreateState();
        state.Players[1].HasFolded = true;

        state.GetPlayersInHand().Should().HaveCount(2);
    }

    [Fact]
    public void AreAllPlayersAllIn_WhenAllIn_ReturnsTrue()
    {
        var state = CreateState();
        foreach (var p in state.Players.Where(p => p.Status == GamePlayerStatus.Active))
        {
            p.IsAllIn = true;
        }

        state.AreAllPlayersAllIn().Should().BeTrue();
    }

    [Fact]
    public void AreAllPlayersAllIn_WhenOneNotAllIn_ReturnsFalse()
    {
        var state = CreateState();
        state.Players[0].IsAllIn = true;
        state.Players[1].IsAllIn = false;

        state.AreAllPlayersAllIn().Should().BeFalse();
    }

    [Fact]
    public void ResetPlayerHandState_ResetsPerHandFields()
    {
        var state = CreateState();
        state.Players[0].HasFolded = true;
        state.Players[0].IsAllIn = true;
        state.Players[0].CurrentBet = 50;
        state.Players[0].TotalContributedThisHand = 100;
        state.Players[0].HasDrawnThisRound = true;
        state.Players[0].DropOrStayDecision = DropOrStayDecision.Stay;

        state.ResetPlayerHandState();

        var p = state.Players[0];
        p.HasFolded.Should().BeFalse();
        p.IsAllIn.Should().BeFalse();
        p.CurrentBet.Should().Be(0);
        p.TotalContributedThisHand.Should().Be(0);
        p.HasDrawnThisRound.Should().BeFalse();
        p.DropOrStayDecision.Should().BeNull();
    }
}
