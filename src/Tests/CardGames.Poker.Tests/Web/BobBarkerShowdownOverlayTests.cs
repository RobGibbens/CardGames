#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardGames.Core.French.Cards;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class BobBarkerShowdownOverlayTests
{
    [Fact]
    public void GetBobBarkerPlayers_OrdersShowcaseRowsByWinnerThenPayoutThenName()
    {
        var overlay = new ShowdownOverlay
        {
            GameTypeCode = "BOBBARKER",
            ShowdownState = new ShowdownPublicDto
            {
                PlayerResults =
                [
                    CreatePlayer("zoe@example.com", showcaseAmountWon: 10, isShowcaseWinner: false, showcaseCardValue: 9),
                    CreatePlayer("amy@example.com", showcaseAmountWon: 25, isShowcaseWinner: true, showcaseCardValue: 10),
                    CreatePlayer("ben@example.com", showcaseAmountWon: 25, isShowcaseWinner: true, showcaseCardValue: 10),
                    CreatePlayer("nina@example.com", showcaseAmountWon: 0, isShowcaseWinner: false, showcaseCard: null, showcaseCardValue: null)
                ]
            }
        };

        var players = InvokeBobBarkerPlayers(overlay).ToList();

        players.Select(player => player.PlayerName).Should().Equal(
            "amy@example.com",
            "ben@example.com",
            "zoe@example.com");
    }

    [Fact]
    public void GetPlayerDisplayName_PrefersFirstNameAndFallsBackToEmailLocalPart()
    {
        var namedPlayer = CreatePlayer("amy@example.com", playerFirstName: "Amy", showcaseAmountWon: 25, isShowcaseWinner: true, showcaseCardValue: 10);
        var unnamedPlayer = CreatePlayer("zoe.turner@example.com", showcaseAmountWon: 10, isShowcaseWinner: false, showcaseCardValue: 9);

        InvokePlayerDisplayName(namedPlayer).Should().Be("Amy");
        InvokePlayerDisplayName(unnamedPlayer).Should().Be("Zoe");
    }

    [Fact]
    public void GetDisplayCardsForShowdown_BobBarker_WithCombinedCards_ReturnsBestFiveWithoutShowcase()
    {
        var overlay = new ShowdownOverlay
        {
            GameTypeCode = "BOBBARKER",
            ShowdownState = new ShowdownPublicDto
            {
                PlayerResults =
                [
                    CreatePlayer(
                        "amy@example.com",
                        showcaseCard: new CardPublicDto
                        {
                            IsFaceUp = true,
                            Rank = "7",
                            Suit = "Spades",
                            DealOrder = 5
                        })
                ]
            }
        };

        var hand = CreateHand("amy@example.com", "Ah Ad Kc 2c 7s Ac Ks Kd 3h 4d");
        var sharedCommunityCards = ParseShowdownCards("Ac Ks Kd 3h 4d");

        var cards = InvokeShowdownDisplayCards(overlay, hand, sharedCommunityCards);

        cards.Should().HaveCount(5);
        cards.Should().Contain(new ShowdownCard(CardSuit.Hearts, CardSymbol.Ace));
        cards.Should().Contain(new ShowdownCard(CardSuit.Diamonds, CardSymbol.Ace));
        cards.Should().Contain(new ShowdownCard(CardSuit.Clubs, CardSymbol.Ace));
        cards.Should().Contain(new ShowdownCard(CardSuit.Spades, CardSymbol.King));
        cards.Should().Contain(new ShowdownCard(CardSuit.Diamonds, CardSymbol.King));
        cards.Should().NotContain(new ShowdownCard(CardSuit.Spades, CardSymbol.Seven));
    }

    [Fact]
    public void GetDisplayCardsForShowdown_BobBarker_WithSeparatedCommunity_ReturnsBestFive()
    {
        var overlay = new ShowdownOverlay
        {
            GameTypeCode = "BOBBARKER",
            ShowdownState = new ShowdownPublicDto
            {
                PlayerResults =
                [
                    CreatePlayer(
                        "amy@example.com",
                        showcaseCard: new CardPublicDto
                        {
                            IsFaceUp = true,
                            Rank = "7",
                            Suit = "Spades",
                            DealOrder = 5
                        })
                ]
            }
        };

        var hand = CreateHand("amy@example.com", "Ah Ad Kc 2c");
        var sharedCommunityCards = ParseShowdownCards("Ac Ks Kd 3h 4d");

        var cards = InvokeShowdownDisplayCards(overlay, hand, sharedCommunityCards);

        cards.Should().HaveCount(5);
        cards.Should().Contain(new ShowdownCard(CardSuit.Hearts, CardSymbol.Ace));
        cards.Should().Contain(new ShowdownCard(CardSuit.Diamonds, CardSymbol.Ace));
        cards.Should().Contain(new ShowdownCard(CardSuit.Clubs, CardSymbol.Ace));
        cards.Should().Contain(new ShowdownCard(CardSuit.Spades, CardSymbol.King));
        cards.Should().Contain(new ShowdownCard(CardSuit.Diamonds, CardSymbol.King));
    }

    private static IEnumerable<ShowdownPlayerResultDto> InvokeBobBarkerPlayers(ShowdownOverlay overlay)
    {
        var method = typeof(ShowdownOverlay).GetMethod("GetBobBarkerPlayers", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay.GetBobBarkerPlayers should exist for Bob Barker rendering");

        var result = method!.Invoke(overlay, null);
        result.Should().NotBeNull();
        return result.Should().BeAssignableTo<IEnumerable<ShowdownPlayerResultDto>>().Subject;
    }

    private static string InvokePlayerDisplayName(ShowdownPlayerResultDto player)
    {
        var method = typeof(ShowdownOverlay).GetMethod("GetPlayerDisplayName", BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay.GetPlayerDisplayName should exist for Bob Barker rendering");

        var result = method!.Invoke(null, [player]);
        result.Should().BeOfType<string>();
        return (string)result!;
    }

    private static IReadOnlyList<ShowdownCard> InvokeShowdownDisplayCards(
        ShowdownOverlay overlay,
        ShowdownPlayerHand hand,
        IReadOnlyList<ShowdownCard> sharedCommunityCards)
    {
        var method = typeof(ShowdownOverlay).GetMethod(
            "GetDisplayCardsForShowdown",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay should normalize Bob Barker showdown cards before rendering");

        var result = method!.Invoke(overlay, [hand, sharedCommunityCards, false]);
        result.Should().NotBeNull();

        var tuples = result!
            .Should()
            .BeAssignableTo<IEnumerable<(ShowdownCard, bool)>>()
            .Subject
            .ToList();

        return tuples.Select(tuple => tuple.Item1).ToList();
    }

    private static ShowdownPlayerHand CreateHand(string playerName, string cards)
    {
        return new ShowdownPlayerHand(
            amountWon: 0,
            cards: ParseShowdownCards(cards).ToList(),
            handDescription: string.Empty,
            handStrength: null,
            handType: string.Empty,
            highHandAmountWon: 0,
            isHighHandWinner: false,
            isSevensWinner: false,
            isWinner: false,
            playerFirstName: playerName,
            playerName: playerName,
            sevensAmountWon: 0,
            wildCardIndexes: []);
    }

    private static IReadOnlyList<ShowdownCard> ParseShowdownCards(string cards)
    {
        return cards
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseShowdownCard)
            .ToList();
    }

    private static ShowdownCard ParseShowdownCard(string token)
    {
        token.Should().NotBeNullOrWhiteSpace();
        var trimmed = token.Trim();

        var suit = trimmed[^1] switch
        {
            'h' or 'H' => CardSuit.Hearts,
            'd' or 'D' => CardSuit.Diamonds,
            'c' or 'C' => CardSuit.Clubs,
            's' or 'S' => CardSuit.Spades,
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown suit")
        };

        var rankText = trimmed[..^1].ToUpperInvariant();
        var symbol = rankText switch
        {
            "A" => CardSymbol.Ace,
            "K" => CardSymbol.King,
            "Q" => CardSymbol.Queen,
            "J" => CardSymbol.Jack,
            "10" or "T" => CardSymbol.Ten,
            "9" => CardSymbol.Nine,
            "8" => CardSymbol.Eight,
            "7" => CardSymbol.Seven,
            "6" => CardSymbol.Six,
            "5" => CardSymbol.Five,
            "4" => CardSymbol.Four,
            "3" => CardSymbol.Three,
            "2" => CardSymbol.Deuce,
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown rank")
        };

        return new ShowdownCard(suit, symbol);
    }

    private static ShowdownPlayerResultDto CreatePlayer(
        string playerName,
        string? playerFirstName = null,
        int showcaseAmountWon = 0,
        bool isShowcaseWinner = false,
        int? showcaseCardValue = null,
        CardPublicDto? showcaseCard = null)
    {
        return new ShowdownPlayerResultDto
        {
            PlayerName = playerName,
            PlayerFirstName = playerFirstName,
            Cards = [],
            ShowcaseAmountWon = showcaseAmountWon,
            IsShowcaseWinner = isShowcaseWinner,
            ShowcaseCardValue = showcaseCardValue,
            ShowcaseCard = showcaseCard ?? (showcaseCardValue.HasValue
                ? new CardPublicDto
                {
                    IsFaceUp = true,
                    Rank = showcaseCardValue.Value == 14 ? "A" : showcaseCardValue.Value.ToString(),
                    Suit = "Spades",
                    DealOrder = 5
                }
                : null)
        };
    }
}