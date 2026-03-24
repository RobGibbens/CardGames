using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardGames.Poker.Web.Components.Pages;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using static CardGames.Poker.Web.Components.Pages.TablePlay;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayPairPressureWildCardsTests
{
    [Fact]
    public void GetWildCardsForDisplay_DuringPairPressureDealReveal_UsesOnlyTwoMostRecentPairedRanks()
    {
        var tablePlay = new TablePlay();
        SetPrivateField(tablePlay, "_gameTypeCode", "PAIRPRESSURE");
        SetPrivateField(tablePlay, "_dealAnimationInProgress", true);
        SetPrivateField(tablePlay, "_dealerSeatIndex", 0);
        SetPrivateField(tablePlay, "_seats", new List<SeatInfo>
        {
            CreateSeat(1, "Alice",
                CreateFaceUpCard("8", "Hearts", 1),
                CreateFaceUpCard("5", "Clubs", 3),
                CreateFaceUpCard("K", "Hearts", 5)),
            CreateSeat(2, "Bob",
                CreateFaceUpCard("8", "Diamonds", 2),
                CreateFaceUpCard("5", "Spades", 4),
                CreateFaceUpCard("K", "Diamonds", 6))
        });

        var wildCards = InvokeGetWildCardsForDisplay(tablePlay);

        wildCards.Select(card => card.Label).Should().Equal("Fives", "Kings");
        wildCards.Select(card => card.Rank).Should().Equal("5", "K");
    }

    private static IReadOnlyList<TableCanvas.WildCardDisplay> InvokeGetWildCardsForDisplay(TablePlay tablePlay)
    {
        var method = typeof(TablePlay).GetMethod("GetWildCardsForDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("TablePlay should expose GetWildCardsForDisplay for the table wild-card panel");

        var result = method!.Invoke(tablePlay, null);
        result.Should().NotBeNull();
        return result.Should().BeAssignableTo<IReadOnlyList<TableCanvas.WildCardDisplay>>().Subject;
    }

    private static void SetPrivateField<T>(TablePlay tablePlay, string fieldName, T value)
    {
        var field = typeof(TablePlay).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"TablePlay should keep {fieldName} as a private field");
        field!.SetValue(tablePlay, value);
    }

    private static SeatInfo CreateSeat(int seatIndex, string playerName, params CardInfo[] cards) => new()
    {
        SeatIndex = seatIndex,
        IsOccupied = true,
        PlayerName = playerName,
        Cards = cards.ToList()
    };

    private static CardInfo CreateFaceUpCard(string rank, string suit, int dealOrder) => new()
    {
        Rank = rank,
        Suit = suit,
        DealOrder = dealOrder,
        IsFaceUp = true,
        IsPubliclyVisible = true
    };
}