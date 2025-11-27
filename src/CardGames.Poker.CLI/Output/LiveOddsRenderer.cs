using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using Spectre.Console;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Renders live poker hand odds in the CLI using Spectre.Console.
/// Displays hand type probabilities for each poker variant.
/// </summary>
internal static class LiveOddsRenderer
{
    /// <summary>
    /// Renders the odds display for a Texas Hold'em hand.
    /// </summary>
    public static void RenderHoldemOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateHoldemOdds(
                    heroHoleCards,
                    communityCards,
                    deadCards);

                RenderOddsTable(odds, "Texas Hold'em Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for a Seven Card Stud hand.
    /// </summary>
    public static void RenderStudOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateStudOdds(
                    heroHoleCards,
                    heroBoardCards,
                    deadCards: deadCards);

                RenderOddsTable(odds, "Seven Card Stud Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for a Baseball hand (3s and 9s are wild).
    /// </summary>
    public static void RenderBaseballOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateBaseballOdds(
                    heroHoleCards,
                    heroBoardCards,
                    deadCards: deadCards);

                RenderOddsTable(odds, "Baseball Odds (3s & 9s Wild)");
            });
    }

    /// <summary>
    /// Renders the odds display for a Follow The Queen hand.
    /// </summary>
    public static void RenderFollowTheQueenOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        IReadOnlyCollection<Card> faceUpCardsInOrder,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateFollowTheQueenOdds(
                    heroHoleCards,
                    heroBoardCards,
                    faceUpCardsInOrder,
                    deadCards: deadCards);

                RenderOddsTable(odds, "Follow The Queen Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for a Kings and Lows hand.
    /// </summary>
    public static void RenderKingsAndLowsOdds(
        IReadOnlyCollection<Card> heroCards,
        bool kingRequired = false,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateKingsAndLowsOdds(
                    heroCards,
                    kingRequired,
                    deadCards);

                RenderOddsTable(odds, "Kings and Lows Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for a Five Card Draw hand.
    /// </summary>
    public static void RenderDrawOdds(
        IReadOnlyCollection<Card> heroCards,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateDrawOdds(
                    heroCards,
                    deadCards);

                RenderOddsTable(odds, "Five Card Draw Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for an Omaha hand.
    /// </summary>
    public static void RenderOmahaOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        IReadOnlyCollection<Card> deadCards = null)
    {
        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateOmahaOdds(
                    heroHoleCards,
                    communityCards,
                    deadCards);

                RenderOddsTable(odds, "Omaha Odds");
            });
    }

    /// <summary>
    /// Renders the odds result as a formatted table.
    /// </summary>
    private static void RenderOddsTable(OddsCalculator.OddsResult odds, string title)
    {
        var panel = new Panel(BuildOddsContent(odds))
            .Header($"[bold cyan]{title}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
    }

    private static string BuildOddsContent(OddsCalculator.OddsResult odds)
    {
        var lines = new List<string>();

        lines.Add("[bold yellow]Hand Probabilities[/]");
        
        // Order hand types from best to worst, including Five of a Kind for wild card games
        var orderedHandTypes = new[]
        {
            HandType.FiveOfAKind,
            HandType.StraightFlush,
            HandType.Quads,
            HandType.FullHouse,
            HandType.Flush,
            HandType.Straight,
            HandType.Trips,
            HandType.TwoPair,
            HandType.OnePair,
            HandType.HighCard
        };

        foreach (var handType in orderedHandTypes)
        {
            if (odds.HandTypeProbabilities.TryGetValue(handType, out var probability))
            {
                var handName = GetHandTypeName(handType);
                var barLength = probability > 0 ? System.Math.Max(1, (int)(probability * 20)) : 0;
                var bar = new string('█', barLength);
                var color = GetHandTypeColor(handType);
                lines.Add($"  [{color}]{handName,-16}[/] {FormatPercentage(probability)} [{color}]{bar}[/]");
            }
        }

        return string.Join("\n", lines);
    }

    private static string FormatPercentage(decimal value)
    {
        return $"{value * 100:F1}%".PadLeft(6);
    }

    private static string GetHandTypeName(HandType handType)
    {
        return handType switch
        {
            HandType.HighCard => "High Card",
            HandType.OnePair => "One Pair",
            HandType.TwoPair => "Two Pair",
            HandType.Trips => "Three of a Kind",
            HandType.Straight => "Straight",
            HandType.Flush => "Flush",
            HandType.FullHouse => "Full House",
            HandType.Quads => "Four of a Kind",
            HandType.StraightFlush => "Straight Flush",
            HandType.FiveOfAKind => "Five of a Kind",
            _ => handType.ToString()
        };
    }

    private static string GetHandTypeColor(HandType handType)
    {
        return handType switch
        {
            HandType.FiveOfAKind => "fuchsia",
            HandType.StraightFlush => "magenta",
            HandType.Quads => "red",
            HandType.FullHouse => "darkorange",
            HandType.Flush => "blue",
            HandType.Straight => "green",
            HandType.Trips => "cyan",
            HandType.TwoPair => "yellow",
            HandType.OnePair => "white",
            HandType.HighCard => "dim",
            _ => "white"
        };
    }
}
