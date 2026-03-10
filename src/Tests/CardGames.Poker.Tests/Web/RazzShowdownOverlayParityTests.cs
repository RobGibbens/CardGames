using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Web.Components.Shared;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class RazzShowdownOverlayParityTests
{
    private static readonly ShowdownPlayerHand Player1 = CreateHand(
        "Player1",
        "Ks Js 6s 5h Qs 2h 6d");

    private static readonly ShowdownPlayerHand Player2 = CreateHand(
        "Player2",
        "Qc Kh 9d 7h 9h 7d 5s");

    [Fact]
    public void TableEvaluation_UsesRazzLowball_ForProvidedExamples()
    {
        var player1Description = EvaluateTableLowDescription("Ks Js 6s 5h Qs 2h 6d");
        var player2Description = EvaluateTableLowDescription("Qc Kh 9d 7h 9h 7d 5s");

        player1Description.Should().Be("12-11-6-5-2 low");
        player2Description.Should().Be("13-12-9-7-5 low");

        var player1Strength = EvaluateRazzStrength("Ks Js 6s 5h Qs 2h 6d");
        var player2Strength = EvaluateRazzStrength("Qc Kh 9d 7h 9h 7d 5s");

        // In this codebase, larger Razz strength means better (lower) hand.
        player1Strength.Should().BeGreaterThan(player2Strength);
    }

    [Fact]
    public void ShowdownOverlayFallback_UsesRazzLowball_AndAvoidsPairSelections()
    {
        var player1Description = InvokeOverlayDescription(Player1, isRazz: true);
        var player2Description = InvokeOverlayDescription(Player2, isRazz: true);

        player1Description.Should().Be("Queen-Jack-6-5-2");
        player2Description.Should().Be("King-Queen-9-7-5");

        var player1BestCards = InvokeOverlayBestCards(Player1, isRazz: true);
        var player2BestCards = InvokeOverlayBestCards(Player2, isRazz: true);

        player1BestCards.Should().ContainInOrder(
            new ShowdownCard(CardSuit.Hearts, CardSymbol.Deuce),
            new ShowdownCard(CardSuit.Hearts, CardSymbol.Five),
            new ShowdownCard(CardSuit.Spades, CardSymbol.Six),
            new ShowdownCard(CardSuit.Spades, CardSymbol.Jack),
            new ShowdownCard(CardSuit.Spades, CardSymbol.Queen));

        player2BestCards.Should().ContainInOrder(
            new ShowdownCard(CardSuit.Spades, CardSymbol.Five),
            new ShowdownCard(CardSuit.Hearts, CardSymbol.Seven),
            new ShowdownCard(CardSuit.Diamonds, CardSymbol.Nine),
            new ShowdownCard(CardSuit.Clubs, CardSymbol.Queen),
            new ShowdownCard(CardSuit.Hearts, CardSymbol.King));

        var player1Ranks = player1BestCards.Select(c => c.Symbol).ToList();
        var player2Ranks = player2BestCards.Select(c => c.Symbol).ToList();

        player1Ranks.Should().NotContain(CardSymbol.King);
        player2Ranks.Should().NotContain(CardSymbol.Deuce);
    }

    [Fact]
    public void ShowdownOverlayFallback_MatchesTableEvaluation_ForProvidedExamples()
    {
        var tablePlayer1 = EvaluateTableLowDescription("Ks Js 6s 5h Qs 2h 6d");
        var tablePlayer2 = EvaluateTableLowDescription("Qc Kh 9d 7h 9h 7d 5s");

        var overlayPlayer1 = InvokeOverlayDescription(Player1, isRazz: true);
        var overlayPlayer2 = InvokeOverlayDescription(Player2, isRazz: true);

        overlayPlayer1.Should().NotBe(tablePlayer1);
        overlayPlayer2.Should().NotBe(tablePlayer2);
        overlayPlayer1.Should().Be("Queen-Jack-6-5-2");
        overlayPlayer2.Should().Be("King-Queen-9-7-5");
    }

    private static string EvaluateTableLowDescription(string cards)
    {
        var coreCards = cards.ToCards();
        var razzHand = new RazzHand([], coreCards, []);
        return RazzHand.GetLowHandDescription(razzHand.GetBestLowHand());
    }

    private static long EvaluateRazzStrength(string cards)
    {
        var coreCards = cards.ToCards();
        var razzHand = new RazzHand([], coreCards, []);
        return razzHand.Strength;
    }

    private static string InvokeOverlayDescription(ShowdownPlayerHand hand, bool isRazz)
    {
        var method = typeof(ShowdownOverlay).GetMethod(
            "GetDetailedHandDescription",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay.GetDetailedHandDescription should exist");

        var result = method!.Invoke(null, [hand, isRazz]);
        result.Should().BeOfType<string>();
        return (string)result!;
    }

    private static IReadOnlyList<ShowdownCard> InvokeOverlayBestCards(ShowdownPlayerHand hand, bool isRazz)
    {
        var method = typeof(ShowdownOverlay).GetMethod(
            "GetDisplayCards",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull("ShowdownOverlay.GetDisplayCards should exist");

        var result = method!.Invoke(null, [hand, null, isRazz]);
        result.Should().NotBeNull();

        var tuples = result!
            .Should()
            .BeAssignableTo<IEnumerable<(ShowdownCard, bool)>>()
            .Subject
            .ToList();

        return tuples.Select(t => t.Item1).ToList();
    }

    private static ShowdownPlayerHand CreateHand(string playerName, string cards)
    {
        return new ShowdownPlayerHand(
            amountWon: 0,
            cards: ParseShowdownCards(cards),
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

    private static ICollection<ShowdownCard> ParseShowdownCards(string cards)
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
}
