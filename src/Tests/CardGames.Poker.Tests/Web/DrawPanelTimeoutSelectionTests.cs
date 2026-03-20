#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class DrawPanelTimeoutSelectionTests
{
    [Fact]
    public void GetTimeoutSelectionIndices_UsesExistingValidSelection()
    {
        var panel = new DrawPanel
        {
            Cards =
            [
                CreateCard("2", "Clubs"),
                CreateCard("5", "Hearts"),
                CreateCard("K", "Spades")
            ],
            SelectedIndices = [1],
            MinDiscards = 1,
            MaxDiscards = 1,
            AutoSelectLowestCardOnTimeout = true
        };

        var result = InvokeTimeoutSelectionIndices(panel);

        result.Should().Equal(1);
    }

    [Fact]
    public void GetLowestTimeoutSelectionIndex_SkipsLowestPair()
    {
        var cards = new List<TablePlay.CardInfo>
        {
            CreateCard("2", "Clubs"),
            CreateCard("2", "Diamonds"),
            CreateCard("5", "Hearts"),
            CreateCard("9", "Spades"),
            CreateCard("K", "Clubs")
        };

        var result = InvokeLowestTimeoutSelectionIndex(cards);

        result.Should().Be(2);
    }

    [Fact]
    public void GetLowestTimeoutSelectionIndex_SkipsMultiplePairsUntilSingleton()
    {
        var cards = new List<TablePlay.CardInfo>
        {
            CreateCard("2", "Clubs"),
            CreateCard("2", "Diamonds"),
            CreateCard("5", "Hearts"),
            CreateCard("5", "Spades"),
            CreateCard("9", "Clubs")
        };

        var result = InvokeLowestTimeoutSelectionIndex(cards);

        result.Should().Be(4);
    }

    [Fact]
    public void GetLowestTimeoutSelectionIndex_PrefersBreakingPairOverTripsWhenAllRanksRepeat()
    {
        var cards = new List<TablePlay.CardInfo>
        {
            CreateCard("2", "Clubs"),
            CreateCard("2", "Diamonds"),
            CreateCard("2", "Hearts"),
            CreateCard("5", "Spades"),
            CreateCard("5", "Clubs")
        };

        var result = InvokeLowestTimeoutSelectionIndex(cards);

        result.Should().BeOneOf(3, 4);
    }

    [Fact]
    public void GetLowestTimeoutSelectionIndex_TreatsAceAsLowByDefault()
    {
        var cards = new List<TablePlay.CardInfo>
        {
            CreateCard("A", "Clubs"),
            CreateCard("2", "Diamonds"),
            CreateCard("5", "Hearts"),
            CreateCard("9", "Spades"),
            CreateCard("K", "Clubs")
        };

        var result = InvokeLowestTimeoutSelectionIndex(cards);

        result.Should().Be(0);
    }

    private static List<int> InvokeTimeoutSelectionIndices(DrawPanel panel)
    {
        var method = typeof(DrawPanel).GetMethod("GetTimeoutSelectionIndices", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("DrawPanel should calculate a deterministic timeout selection for Bob Barker");

        var result = method!.Invoke(panel, null);
        result.Should().NotBeNull();
        return result.Should().BeAssignableTo<List<int>>().Subject;
    }

    private static int InvokeLowestTimeoutSelectionIndex(IReadOnlyList<TablePlay.CardInfo> cards)
    {
        var method = typeof(DrawPanel).GetMethod("GetLowestTimeoutSelectionIndex", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("DrawPanel should expose Bob Barker timeout card ordering logic");

        var result = method!.Invoke(null, [cards]);
        result.Should().BeOfType<int>();
        return (int)result!;
    }

    private static TablePlay.CardInfo CreateCard(string rank, string suit)
    {
        return new TablePlay.CardInfo
        {
            Rank = rank,
            Suit = suit,
            IsFaceUp = true,
            IsPubliclyVisible = true
        };
    }
}