using System;
using System.Collections.Generic;
using System.Reflection;
using CardGames.Contracts.SignalR;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using CardGames.Poker.Web.Components.Pages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayCountdownTests
{
    [Fact]
    public void CalculateSecondsUntilDeadline_UsesServerClockOffsetAndCeiling()
    {
        var deadlineUtc = new DateTimeOffset(2026, 4, 5, 12, 0, 10, TimeSpan.Zero);
        var clientUtcNow = new DateTimeOffset(2026, 4, 5, 12, 0, 0, 750, TimeSpan.Zero);
        var serverClockOffset = TimeSpan.FromMilliseconds(1500);

        var result = InvokeCalculateSecondsUntilDeadline(deadlineUtc, clientUtcNow, serverClockOffset);

        result.Should().Be(8);
    }

    [Fact]
    public void ResolveCountdownDeadlineUtc_PrefersNextHandStartWhenPresent()
    {
        var handCompletedAtUtc = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
        var nextHandStartsAtUtc = handCompletedAtUtc.AddSeconds(10);
        var state = CreateTableState(handCompletedAtUtc, nextHandStartsAtUtc);

        var result = InvokeResolveCountdownDeadlineUtc(state, isEndedPhase: true, isGameCompleted: true);

        result.Should().Be(nextHandStartsAtUtc);
    }

    [Fact]
    public void ResolveCountdownDeadlineUtc_UsesCompletedHandTimeForEndedGames()
    {
        var handCompletedAtUtc = new DateTimeOffset(2026, 4, 5, 12, 0, 0, TimeSpan.Zero);
        var state = CreateTableState(handCompletedAtUtc, nextHandStartsAtUtc: null);

        var result = InvokeResolveCountdownDeadlineUtc(state, isEndedPhase: true, isGameCompleted: false);

        result.Should().Be(handCompletedAtUtc.AddSeconds(10));
    }

    [Fact]
    public void CreateRunoutCardInfo_KeepsFaceDownRunoutCardsHidden()
    {
        var runoutCard = new CardPublicDto
        {
            IsFaceUp = false,
            Rank = null,
            Suit = null,
            DealOrder = 7
        };

        var result = InvokeCreateRunoutCardInfo(runoutCard, initialCardCount: 6, runoutCardIndex: 0);

        result.IsFaceUp.Should().BeFalse();
        result.IsPubliclyVisible.Should().BeFalse();
        result.Rank.Should().BeNull();
        result.Suit.Should().BeNull();
        result.IsWild.Should().BeFalse();
        result.DealOrder.Should().Be(6);
    }

    [Fact]
    public void CreateRunoutCardInfo_PreservesFaceUpRunoutCards()
    {
        var runoutCard = new CardPublicDto
        {
            IsFaceUp = true,
            Rank = "4",
            Suit = "Hearts",
            DealOrder = 5
        };

        var result = InvokeCreateRunoutCardInfo(runoutCard, initialCardCount: 3, runoutCardIndex: 1);

        result.IsFaceUp.Should().BeTrue();
        result.IsPubliclyVisible.Should().BeTrue();
        result.Rank.Should().Be("4");
        result.Suit.Should().Be("Hearts");
        result.DealOrder.Should().Be(4);
    }

    [Fact]
    public void UpdateSeatWithRunoutCards_PreservesFaceDownRunoutSlots()
    {
        var tablePlay = new TablePlay();
        SetLogger(tablePlay);

        SetPrivateField(tablePlay, "_seats", new List<SeatInfo>
        {
            new()
            {
                SeatIndex = 0,
                IsOccupied = true,
                Cards =
                [
                    new CardInfo { Rank = null, Suit = null, IsFaceUp = false, IsPubliclyVisible = false, DealOrder = 0 },
                    new CardInfo { Rank = null, Suit = null, IsFaceUp = false, IsPubliclyVisible = false, DealOrder = 1 },
                    new CardInfo { Rank = "A", Suit = "Spades", IsFaceUp = true, IsPubliclyVisible = true, DealOrder = 2 }
                ]
            }
        });

        SetPrivateField(tablePlay, "_allInRunoutState", new AllInRunoutStateDto
        {
            IsActive = true,
            RunoutCardsBySeat = new Dictionary<int, IReadOnlyList<CardPublicDto>>
            {
                [0] =
                [
                    new CardPublicDto { IsFaceUp = true, Rank = "4", Suit = "Hearts", DealOrder = 3 },
                    new CardPublicDto { IsFaceUp = true, Rank = "5", Suit = "Clubs", DealOrder = 4 },
                    new CardPublicDto { IsFaceUp = false, Rank = null, Suit = null, DealOrder = 5 },
                    new CardPublicDto { IsFaceUp = true, Rank = "7", Suit = "Diamonds", DealOrder = 6 }
                ]
            }
        });

        SetPrivateField(tablePlay, "_runoutInitialCardCounts", new Dictionary<int, int> { [0] = 3 });

        InvokeUpdateSeatWithRunoutCards(tablePlay, seatIndex: 0, visibleRunoutCards: 4);

        var seats = GetPrivateField<List<SeatInfo>>(tablePlay, "_seats");
        seats[0].Cards.Should().HaveCount(7);
        seats[0].Cards[5].IsFaceUp.Should().BeFalse();
        seats[0].Cards[5].IsPubliclyVisible.Should().BeFalse();
        seats[0].Cards[5].Rank.Should().BeNull();
        seats[0].Cards[5].Suit.Should().BeNull();
        seats[0].Cards[6].IsFaceUp.Should().BeTrue();
        seats[0].Cards[6].IsPubliclyVisible.Should().BeTrue();
        seats[0].Cards[6].Rank.Should().Be("7");
        seats[0].Cards[6].Suit.Should().Be("Diamonds");
    }

    private static int InvokeCalculateSecondsUntilDeadline(
        DateTimeOffset? deadlineUtc,
        DateTimeOffset clientUtcNow,
        TimeSpan serverClockOffset)
    {
        var method = typeof(TablePlay).GetMethod("CalculateSecondsUntilDeadline", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should calculate countdown seconds from the server-authored deadline");

        var result = method!.Invoke(null, [deadlineUtc, clientUtcNow, serverClockOffset]);
        result.Should().BeOfType<int>();
        return (int)result!;
    }

    private static DateTimeOffset? InvokeResolveCountdownDeadlineUtc(
        TableStatePublicDto state,
        bool isEndedPhase,
        bool isGameCompleted)
    {
        var method = typeof(TablePlay).GetMethod("ResolveCountdownDeadlineUtc", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should resolve countdown deadlines from shared server timestamps");

        var result = method!.Invoke(null, [state, isEndedPhase, isGameCompleted]);
        return result.Should().BeAssignableTo<DateTimeOffset?>().Subject;
    }

    private static CardInfo InvokeCreateRunoutCardInfo(CardPublicDto runoutCard, int initialCardCount, int runoutCardIndex)
    {
        var method = typeof(TablePlay).GetMethod("CreateRunoutCardInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should preserve server-authored runout card visibility");

        var tablePlay = new TablePlay();
        var result = method!.Invoke(tablePlay, [runoutCard, initialCardCount, runoutCardIndex]);
        return result.Should().BeOfType<CardInfo>().Subject;
    }

    private static void InvokeUpdateSeatWithRunoutCards(TablePlay tablePlay, int seatIndex, int visibleRunoutCards)
    {
        var method = typeof(TablePlay).GetMethod("UpdateSeatWithRunoutCards", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("TablePlay should build runout seat cards in the server-authored deal order");

        method!.Invoke(tablePlay, [seatIndex, visibleRunoutCards]);
    }

    private static void SetLogger(TablePlay tablePlay)
    {
        var property = typeof(TablePlay).GetProperty("Logger", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull("TablePlay should expose the injected logger property");
        property!.SetValue(tablePlay, NullLogger<TablePlay>.Instance);
    }

    private static void SetPrivateField<T>(TablePlay tablePlay, string fieldName, T value)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        field!.SetValue(tablePlay, value);
    }

    private static T GetPrivateField<T>(TablePlay tablePlay, string fieldName)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        var value = field!.GetValue(tablePlay);
        return value.Should().BeOfType<T>().Subject;
    }

    private static TableStatePublicDto CreateTableState(DateTimeOffset? handCompletedAtUtc, DateTimeOffset? nextHandStartsAtUtc)
    {
        return new TableStatePublicDto
        {
            GameId = Guid.NewGuid(),
            CurrentPhase = "Complete",
            Seats = new List<SeatPublicDto>(),
            HandCompletedAtUtc = handCompletedAtUtc,
            NextHandStartsAtUtc = nextHandStartsAtUtc,
            ServerUtcNow = handCompletedAtUtc ?? DateTimeOffset.UtcNow
        };
    }
}