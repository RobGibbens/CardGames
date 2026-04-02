using System;
using System.Collections.Generic;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.InMemoryEngine;

public class RuntimeStateExtensionsTests
{
    private static ActiveGameRuntimeState CreateState(int handNumber = 1)
    {
        var gameId = Guid.NewGuid();
        return new ActiveGameRuntimeState
        {
            GameId = gameId,
            CurrentHandNumber = handNumber,
            Status = GameStatus.InProgress,
            Ante = 10,
            Players =
            [
                new RuntimeGamePlayer { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), SeatPosition = 0, Status = GamePlayerStatus.Active, ChipStack = 1000 },
                new RuntimeGamePlayer { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), SeatPosition = 1, Status = GamePlayerStatus.Active, ChipStack = 500 },
                new RuntimeGamePlayer { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), SeatPosition = 2, Status = GamePlayerStatus.Active, ChipStack = 200, HasFolded = true },
            ],
        };
    }

    // ── Player Queries ──

    [Fact]
    public void GetPlayerById_ExistingId_ReturnsPlayer()
    {
        var state = CreateState();
        var target = state.Players[1];

        state.GetPlayerById(target.Id).Should().BeSameAs(target);
    }

    [Fact]
    public void GetPlayerById_NonExistentId_ReturnsNull()
    {
        var state = CreateState();
        state.GetPlayerById(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GetPlayerByPlayerId_ReturnsCorrectPlayer()
    {
        var state = CreateState();
        var target = state.Players[0];

        state.GetPlayerByPlayerId(target.PlayerId).Should().BeSameAs(target);
    }

    [Fact]
    public void GetPlayerBySeat_ReturnsCorrectPlayer()
    {
        var state = CreateState();
        state.GetPlayerBySeat(1).Should().BeSameAs(state.Players[1]);
    }

    [Fact]
    public void GetPlayersInHand_ExcludesFolded()
    {
        var state = CreateState(); // player at seat 2 has HasFolded = true
        var inHand = state.GetPlayersInHand();

        inHand.Should().HaveCount(2);
        inHand.Should().NotContain(p => p.HasFolded);
    }

    [Fact]
    public void GetCurrentActor_WithActiveBettingRound_ReturnsByRoundIndex()
    {
        var state = CreateState();
        var round = new RuntimeBettingRound
        {
            Id = Guid.NewGuid(),
            HandNumber = 1,
            CurrentActorIndex = 1,
            IsComplete = false,
        };
        state.BettingRounds.Add(round);

        state.GetCurrentActor()!.SeatPosition.Should().Be(1);
    }

    [Fact]
    public void GetCurrentActor_NoBettingRound_UseCurrentPlayerIndex()
    {
        var state = CreateState();
        state.CurrentPlayerIndex = 0;

        state.GetCurrentActor()!.SeatPosition.Should().Be(0);
    }

    // ── Betting Round Queries ──

    [Fact]
    public void GetActiveBettingRound_ReturnsLatestIncomplete()
    {
        var state = CreateState();
        var completed = new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 1, RoundNumber = 1, IsComplete = true };
        var active = new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 1, RoundNumber = 2, IsComplete = false };
        state.BettingRounds.AddRange([completed, active]);

        state.GetActiveBettingRound().Should().BeSameAs(active);
    }

    [Fact]
    public void GetActiveBettingRound_AllComplete_ReturnsNull()
    {
        var state = CreateState();
        state.BettingRounds.Add(new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 1, RoundNumber = 1, IsComplete = true });

        state.GetActiveBettingRound().Should().BeNull();
    }

    [Fact]
    public void GetBettingRoundsForCurrentHand_FiltersAndOrdersByRoundNumber()
    {
        var state = CreateState(handNumber: 2);
        state.BettingRounds.Add(new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 1, RoundNumber = 1 }); // different hand
        state.BettingRounds.Add(new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 2, RoundNumber = 2 });
        state.BettingRounds.Add(new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 2, RoundNumber = 1 });

        var rounds = state.GetBettingRoundsForCurrentHand();

        rounds.Should().HaveCount(2);
        rounds[0].RoundNumber.Should().Be(1);
        rounds[1].RoundNumber.Should().Be(2);
    }

    // ── Pot Queries ──

    [Fact]
    public void GetMainPot_ReturnsMainPotForCurrentHand()
    {
        var state = CreateState(handNumber: 1);
        var mainPot = new RuntimePot { Id = Guid.NewGuid(), HandNumber = 1, PotOrder = 0, PotType = PotType.Main, Amount = 100 };
        var sidePot = new RuntimePot { Id = Guid.NewGuid(), HandNumber = 1, PotOrder = 1, PotType = PotType.Side, Amount = 50 };
        state.Pots.AddRange([mainPot, sidePot]);

        state.GetMainPot().Should().BeSameAs(mainPot);
    }

    [Fact]
    public void GetTotalPotAmount_SumsAllPotsForCurrentHand()
    {
        var state = CreateState(handNumber: 1);
        state.Pots.Add(new RuntimePot { Id = Guid.NewGuid(), HandNumber = 1, PotOrder = 0, Amount = 100 });
        state.Pots.Add(new RuntimePot { Id = Guid.NewGuid(), HandNumber = 1, PotOrder = 1, Amount = 50 });
        state.Pots.Add(new RuntimePot { Id = Guid.NewGuid(), HandNumber = 2, PotOrder = 0, Amount = 200 }); // different hand

        state.GetTotalPotAmount().Should().Be(150);
    }

    // ── Card Queries ──

    [Fact]
    public void GetPlayerCards_ReturnsNonDiscardedCardsForPlayer()
    {
        var state = CreateState();
        var playerId = state.Players[0].Id;
        state.Cards.Add(new RuntimeCard { Id = Guid.NewGuid(), GamePlayerId = playerId, HandNumber = 1, DealOrder = 1, IsDiscarded = false });
        state.Cards.Add(new RuntimeCard { Id = Guid.NewGuid(), GamePlayerId = playerId, HandNumber = 1, DealOrder = 2, IsDiscarded = true }); // discarded
        state.Cards.Add(new RuntimeCard { Id = Guid.NewGuid(), GamePlayerId = playerId, HandNumber = 1, DealOrder = 3, IsDiscarded = false });

        var cards = state.GetPlayerCards(playerId);
        cards.Should().HaveCount(2);
        cards[0].DealOrder.Should().Be(1);
        cards[1].DealOrder.Should().Be(3);
    }

    [Fact]
    public void GetCommunityCards_ReturnsOnlyCommunityCardsForCurrentHand()
    {
        var state = CreateState();
        state.Cards.Add(new RuntimeCard { Id = Guid.NewGuid(), Location = CardLocation.Community, HandNumber = 1, DealOrder = 1 });
        state.Cards.Add(new RuntimeCard { Id = Guid.NewGuid(), Location = CardLocation.Hole, HandNumber = 1, DealOrder = 2 });
        state.Cards.Add(new RuntimeCard { Id = Guid.NewGuid(), Location = CardLocation.Community, HandNumber = 2, DealOrder = 1 }); // different hand

        state.GetCommunityCards().Should().HaveCount(1);
    }

    // ── Mutations ──

    [Fact]
    public void CreateMainPot_AddsPotToState()
    {
        var state = CreateState();
        var now = DateTimeOffset.UtcNow;

        var pot = state.CreateMainPot(1, now);

        pot.PotType.Should().Be(PotType.Main);
        pot.PotOrder.Should().Be(0);
        pot.Amount.Should().Be(0);
        pot.GameId.Should().Be(state.GameId);
        state.Pots.Should().Contain(pot);
    }

    [Fact]
    public void CreateBettingRound_AddsRoundToState()
    {
        var state = CreateState();
        var now = DateTimeOffset.UtcNow;

        var round = state.CreateBettingRound("PreFlop", 1, 10, 3, 3, 0, now);

        round.Street.Should().Be("PreFlop");
        round.RoundNumber.Should().Be(1);
        round.MinBet.Should().Be(10);
        round.MaxRaises.Should().Be(3);
        round.PlayersInHand.Should().Be(3);
        round.CurrentActorIndex.Should().Be(0);
        state.BettingRounds.Should().Contain(round);
    }

    [Fact]
    public void RecordBettingAction_AddsActionAndIncrementsPlayersActed()
    {
        var round = new RuntimeBettingRound { Id = Guid.NewGuid(), HandNumber = 1, PlayersActed = 0 };
        var player = new RuntimeGamePlayer { Id = Guid.NewGuid(), ChipStack = 900 };

        var action = round.RecordBettingAction(
            player, BettingActionType.Call, 100, 100, 200, 300, DateTimeOffset.UtcNow);

        action.ActionType.Should().Be(BettingActionType.Call);
        action.Amount.Should().Be(100);
        action.ChipsMoved.Should().Be(100);
        action.ChipStackAfter.Should().Be(900);
        action.PotBefore.Should().Be(200);
        action.PotAfter.Should().Be(300);
        round.PlayersActed.Should().Be(1);
        round.Actions.Should().HaveCount(1);
    }

    [Fact]
    public void FindNextActivePlayerIndex_WrapsAround()
    {
        var state = CreateState(); // seats 0, 1, 2 (seat 2 folded)

        // From seat 1, next non-folded non-all-in active player is seat 0 (wraps)
        state.FindNextActivePlayerIndex(1).Should().Be(0);
    }

    [Fact]
    public void FindNextActivePlayerIndex_ReturnsNextSeat()
    {
        var state = CreateState(); // seat 2 folded
        // From seat 0, next is seat 1
        state.FindNextActivePlayerIndex(0).Should().Be(1);
    }

    [Fact]
    public void FindNextActivePlayerIndex_AllFoldedOrAllIn_ReturnsNegativeOne()
    {
        var state = CreateState();
        foreach (var p in state.Players)
        {
            p.HasFolded = true;
        }

        state.FindNextActivePlayerIndex(0).Should().Be(-1);
    }
}
