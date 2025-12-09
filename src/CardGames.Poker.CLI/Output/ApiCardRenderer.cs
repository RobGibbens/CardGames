using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Contracts;
using Spectre.Console;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Renders playing cards as ASCII art in the CLI using Spectre.Console.
/// </summary>
internal static class ApiCardRenderer
{
    private const int CardSpacing = 1;
    private const string CardBackColor = "blue";

    /// <summary>
    /// Renders a collection of cards side-by-side showing their faces.
    /// </summary>
    /// <param name="cards">The cards to render.</param>
    /// <param name="label">Optional label to display above the cards.</param>
    /// <param name="wildCards">Optional collection of wild cards to highlight in green.</param>
    internal static void RenderCards(ICollection<DealtCard> cards, string label = null, IEnumerable<DealtCard> wildCards = null)
    {
        var cardList = cards.ToList();
        if (cardList.Count == 0)
            return;

        if (!string.IsNullOrEmpty(label))
        {
            AnsiConsole.MarkupLine($"[dim]{label}[/]");
        }

        RenderCardsGrid(cardList, showFaces: true, wildCards: wildCards);
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
                lines[lineIndex].Append($"[{CardBackColor}]{Markup.Escape(cardBack[lineIndex])}[/]");
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
    /// <param name="faceUpCards">Cards to show face up.</param>
    /// <param name="faceDownCount">Number of cards to show face down.</param>
    /// <param name="label">Optional label to display above the cards.</param>
    /// <param name="wildCards">Optional collection of wild cards to highlight in green.</param>
    internal static void RenderMixedCards(
        IEnumerable<Card> faceUpCards,
        int faceDownCount,
        string label = null,
        IEnumerable<Card> wildCards = null)
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
                lines[lineIndex].Append($"[{CardBackColor}]{Markup.Escape(cardBack[lineIndex])}[/]");
            }
            cardIndex++;
        }

        // Render face-up cards
        foreach (var card in faceUpList)
        {
            var cardFace = CardAsciiArt.GetCardFace(card);
            var color = CardAsciiArt.GetCardColor(card, wildCards);
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

    private static void RenderCardsGrid(List<DealtCard> cards, bool showFaces, IEnumerable<DealtCard> wildCards = null)
    {
        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
        {
            var card = cards[cardIndex];
            var cardLines = showFaces ? ApiCardAsciiArt.GetCardFace(card) : CardAsciiArt.GetCardBack();
            var color = showFaces ? ApiCardAsciiArt.GetCardColor(card, wildCards) : CardBackColor;

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

    public static void RenderCards(ICollection<ShowdownCard> cards, string label = null, IEnumerable<DealtCard> wildCards = null)
    {
        var cardList = cards.ToList();
        if (cardList.Count == 0)
            return;

        if (!string.IsNullOrEmpty(label))
        {
            AnsiConsole.MarkupLine($"[dim]{label}[/]");
        }

        RenderCardsGrid(cardList, showFaces: true, wildCards: wildCards);
    }

    private static void RenderCardsGrid(List<ShowdownCard> cards, bool showFaces, IEnumerable<DealtCard> wildCards = null)
    {
        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        for (int cardIndex = 0; cardIndex < cards.Count; cardIndex++)
        {
            var card = cards[cardIndex];
            var cardLines = showFaces ? ApiCardAsciiArt.GetCardFace(new DealtCard(0, card.Suit, card.Symbol)) : CardAsciiArt.GetCardBack();
            var color = showFaces ? ApiCardAsciiArt.GetCardColor(new DealtCard(0, card.Suit, card.Symbol), wildCards) : CardBackColor;

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
