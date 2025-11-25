using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CardGames.Core.French.Cards;
using Spectre.Console;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Handles card flip animations in the CLI.
/// </summary>
internal static class CardAnimator
{
    private const int FlipAnimationFrames = 6;
    private const int FrameDelayMs = 80;

    /// <summary>
    /// Animates flipping cards from face-down to face-up, revealing them one at a time.
    /// </summary>
    internal static void AnimateCardFlip(IEnumerable<Card> cards, string label = null)
    {
        var cardList = cards.ToList();
        if (cardList.Count == 0)
            return;

        if (!string.IsNullOrEmpty(label))
        {
            AnsiConsole.MarkupLine($"[dim]{label}[/]");
        }

        // Show all cards face down first
        var currentlyRevealed = new List<Card>();
        var remainingFaceDown = cardList.Count;

        // Initial state: all cards face down
        RenderAnimationFrame(currentlyRevealed, remainingFaceDown);
        Thread.Sleep(200);

        // Flip each card one by one
        foreach (var card in cardList)
        {
            remainingFaceDown--;
            
            // Animate the flip for this card
            AnimateSingleCardFlip(currentlyRevealed, card, remainingFaceDown);
            
            currentlyRevealed.Add(card);
            Thread.Sleep(150);
        }
    }

    /// <summary>
    /// Animates flipping a single card while other cards remain in their current state.
    /// </summary>
    private static void AnimateSingleCardFlip(
        IReadOnlyList<Card> revealedCards,
        Card cardToFlip,
        int remainingFaceDown)
    {
        var flipFrames = GetFlipFrames();

        foreach (var frame in flipFrames)
        {
            ClearLines(CardAsciiArt.Height);
            RenderFlipFrame(revealedCards, frame, cardToFlip, remainingFaceDown);
            Thread.Sleep(FrameDelayMs);
        }

        // Final frame: show the fully revealed card
        ClearLines(CardAsciiArt.Height);
        var allRevealed = revealedCards.Concat(new[] { cardToFlip }).ToList();
        RenderAnimationFrame(allRevealed, remainingFaceDown);
    }

    /// <summary>
    /// Gets the frames for a flip animation (card narrowing then widening).
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<string>> GetFlipFrames()
    {
        return new[]
        {
            // Frame 1: Full back
            CardAsciiArt.GetCardBack(),
            // Frame 2: Narrowing
            new[]
            {
                "┌─────┐  ",
                "│░░░░░│  ",
                "│░░░░░│  ",
                "│░░░░░│  ",
                "│░░░░░│  ",
                "│░░░░░│  ",
                "└─────┘  "
            },
            // Frame 3: Very narrow
            new[]
            {
                " ┌───┐   ",
                " │░░░│   ",
                " │░░░│   ",
                " │░░░│   ",
                " │░░░│   ",
                " │░░░│   ",
                " └───┘   "
            },
            // Frame 4: Thin line (flip point)
            new[]
            {
                "  ┌─┐    ",
                "  │ │    ",
                "  │ │    ",
                "  │ │    ",
                "  │ │    ",
                "  │ │    ",
                "  └─┘    "
            },
            // Frame 5: Starting to show face (narrow)
            new[]
            {
                " ┌───┐   ",
                " │   │   ",
                " │   │   ",
                " │   │   ",
                " │   │   ",
                " │   │   ",
                " └───┘   "
            },
            // Frame 6: Almost full
            new[]
            {
                "┌─────┐  ",
                "│     │  ",
                "│     │  ",
                "│     │  ",
                "│     │  ",
                "│     │  ",
                "└─────┘  "
            }
        };
    }

    private static void RenderFlipFrame(
        IReadOnlyList<Card> revealedCards,
        IReadOnlyList<string> flipFrame,
        Card cardToFlip,
        int remainingFaceDown)
    {
        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        var cardIndex = 0;

        // Render already revealed cards
        foreach (var card in revealedCards)
        {
            var cardFace = CardAsciiArt.GetCardFace(card);
            var color = CardAsciiArt.GetSuitColor(card.Suit);
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(' ');
                }
                lines[lineIndex].Append($"[{color}]{Markup.Escape(cardFace[lineIndex])}[/]");
            }
            cardIndex++;
        }

        // Render the flipping card (animation frame)
        var flipColor = CardAsciiArt.GetSuitColor(cardToFlip.Suit);
        for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
        {
            if (cardIndex > 0)
            {
                lines[lineIndex].Append(' ');
            }
            lines[lineIndex].Append($"[{flipColor}]{Markup.Escape(flipFrame[lineIndex])}[/]");
        }
        cardIndex++;

        // Render remaining face-down cards
        for (int i = 0; i < remainingFaceDown; i++)
        {
            var cardBack = CardAsciiArt.GetCardBack();
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(' ');
                }
                lines[lineIndex].Append($"[blue]{Markup.Escape(cardBack[lineIndex])}[/]");
            }
            cardIndex++;
        }

        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line.ToString());
        }
    }

    private static void RenderAnimationFrame(
        IReadOnlyList<Card> revealedCards,
        int faceDownCount)
    {
        var lines = new List<StringBuilder>();
        for (int i = 0; i < CardAsciiArt.Height; i++)
        {
            lines.Add(new StringBuilder());
        }

        var cardIndex = 0;

        // Render revealed cards
        foreach (var card in revealedCards)
        {
            var cardFace = CardAsciiArt.GetCardFace(card);
            var color = CardAsciiArt.GetSuitColor(card.Suit);
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(' ');
                }
                lines[lineIndex].Append($"[{color}]{Markup.Escape(cardFace[lineIndex])}[/]");
            }
            cardIndex++;
        }

        // Render face-down cards
        for (int i = 0; i < faceDownCount; i++)
        {
            var cardBack = CardAsciiArt.GetCardBack();
            for (int lineIndex = 0; lineIndex < CardAsciiArt.Height; lineIndex++)
            {
                if (cardIndex > 0)
                {
                    lines[lineIndex].Append(' ');
                }
                lines[lineIndex].Append($"[blue]{Markup.Escape(cardBack[lineIndex])}[/]");
            }
            cardIndex++;
        }

        foreach (var line in lines)
        {
            AnsiConsole.MarkupLine(line.ToString());
        }
    }

    private static void ClearLines(int count)
    {
        // Move cursor up and clear each line
        for (int i = 0; i < count; i++)
        {
            AnsiConsole.Write("\x1b[1A"); // Move up
            AnsiConsole.Write("\x1b[2K"); // Clear line
        }
    }
}
