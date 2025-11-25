using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardGames.Core.French.Cards;
using Spectre.Console;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Renders playing cards as ASCII art in the CLI using Spectre.Console.
/// </summary>
internal static class CardRenderer
{
    private const int CardSpacing = 1;

    /// <summary>
    /// Renders a collection of cards side-by-side showing their faces.
    /// </summary>
    internal static void RenderCards(IEnumerable<Card> cards, string label = null)
    {
        var cardList = cards.ToList();
        if (cardList.Count == 0)
            return;

        if (!string.IsNullOrEmpty(label))
        {
            AnsiConsole.MarkupLine($"[dim]{label}[/]");
        }

        RenderCardsGrid(cardList, showFaces: true);
    }

    /// <summary>
    /// Renders a collection of cards side-by-side showing their backs (face down).
    /// </summary>
    internal static void RenderCardsBack(int count, string label = null)
    {
        if (count <= 0)
            return;

        if (!string.IsNullOrEmpty(label))
        {
            AnsiConsole.MarkupLine($"[dim]{label}[/]");
        }

        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        for (int cardIndex = 0; cardIndex < count; cardIndex++)
        {
            var cardBack = CardAsciiArt.GetCardBack();
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(new string(' ', CardSpacing));
                }
                lines[lineIndex].Append($"[blue]{Markup.Escape(cardBack[lineIndex])}[/]");
            }
        }

        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line.ToString());
        }
    }

    /// <summary>
    /// Renders cards with mixed visibility (some face up, some face down).
    /// </summary>
    internal static void RenderMixedCards(
        IEnumerable<Card> faceUpCards,
        int faceDownCount,
        string label = null)
    {
        var faceUpList = faceUpCards.ToList();
        if (faceUpList.Count == 0 && faceDownCount == 0)
            return;

        if (!string.IsNullOrEmpty(label))
        {
            AnsiConsole.MarkupLine($"[dim]{label}[/]");
        }

        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        var cardIndex = 0;

        // Render face-down cards first
        for (int i = 0; i < faceDownCount; i++)
        {
            var cardBack = CardAsciiArt.GetCardBack();
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(new string(' ', CardSpacing));
                }
                lines[lineIndex].Append($"[blue]{Markup.Escape(cardBack[lineIndex])}[/]");
            }
            cardIndex++;
        }

        // Render face-up cards
        foreach (var card in faceUpList)
        {
            var cardFace = CardAsciiArt.GetCardFace(card);
            var color = CardAsciiArt.GetSuitColor(card.Suit);
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(new string(' ', CardSpacing));
                }
                lines[lineIndex].Append($"[{color}]{Markup.Escape(cardFace[lineIndex])}[/]");
            }
            cardIndex++;
        }

        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line.ToString());
        }
    }

    private static void RenderCardsGrid(IReadOnlyList<Card> cards, bool showFaces)
    {
        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
        {
            var card = cards[cardIndex];
            var cardLines = showFaces ? CardAsciiArt.GetCardFace(card) : CardAsciiArt.GetCardBack();
            var color = showFaces ? CardAsciiArt.GetSuitColor(card.Suit) : "blue";

            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(new string(' ', CardSpacing));
                }
                lines[lineIndex].Append($"[{color}]{Markup.Escape(cardLines[lineIndex])}[/]");
            }
        }

        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line.ToString());
        }
    }
}
