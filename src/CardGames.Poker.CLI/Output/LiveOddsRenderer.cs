using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.HandTypes;
using Spectre.Console;

namespace CardGames.Poker.CLI.Output;

/// <summary>
/// Renders live poker hand odds in the CLI using Spectre.Console.
/// Displays hand type probabilities and win/tie/lose chances.
/// </summary>
internal static class LiveOddsRenderer
{
    /// <summary>
    /// Renders the odds display for a Texas Hold'em hand.
    /// </summary>
    public static void RenderHoldemOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        int opponentCount,
        IReadOnlyCollection<Card> deadCards = null)
    {
        if (opponentCount <= 0)
        {
            AnsiConsole.MarkupLine("[dim]No opponents - you will win![/]");
            return;
        }

        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateHoldemOdds(
                    heroHoleCards,
                    communityCards,
                    opponentCount,
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
        IReadOnlyList<IReadOnlyCollection<Card>> opponentBoardCards,
        IReadOnlyCollection<Card> deadCards = null)
    {
        if (opponentBoardCards == null || opponentBoardCards.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No opponents - you will win![/]");
            return;
        }

        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateStudOdds(
                    heroHoleCards,
                    heroBoardCards,
                    opponentBoardCards,
                    deadCards: deadCards);

                RenderOddsTable(odds, "Seven Card Stud Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for a Five Card Draw hand.
    /// </summary>
    public static void RenderDrawOdds(
        IReadOnlyCollection<Card> heroCards,
        int opponentCount,
        IReadOnlyCollection<Card> deadCards = null)
    {
        if (opponentCount <= 0)
        {
            AnsiConsole.MarkupLine("[dim]No opponents - you will win![/]");
            return;
        }

        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateDrawOdds(
                    heroCards,
                    opponentCount,
                    deadCards: deadCards);

                RenderOddsTable(odds, "Five Card Draw Odds");
            });
    }

    /// <summary>
    /// Renders the odds display for an Omaha hand.
    /// </summary>
    public static void RenderOmahaOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        int opponentCount,
        IReadOnlyCollection<Card> deadCards = null)
    {
        if (opponentCount <= 0)
        {
            AnsiConsole.MarkupLine("[dim]No opponents - you will win![/]");
            return;
        }

        AnsiConsole.Status()
            .Start("[yellow]Calculating odds...[/]", ctx =>
            {
                var odds = OddsCalculator.CalculateOmahaOdds(
                    heroHoleCards,
                    communityCards,
                    opponentCount,
                    deadCards);

                RenderOddsTable(odds, "Omaha Odds");
            });
    }

    /// <summary>
    /// Renders the odds result as a formatted table.
    /// </summary>
    private static void RenderOddsTable(OddsCalculator.OddsResult odds, string title)
    {
        // Create the main panel
        var panel = new Panel(BuildOddsContent(odds))
            .Header($"[bold cyan]{title}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Cyan1);

        AnsiConsole.Write(panel);
    }

    private static string BuildOddsContent(OddsCalculator.OddsResult odds)
    {
        var lines = new List<string>();

        // Outcome probabilities section
        lines.Add("[bold yellow]Win/Tie/Lose Probabilities[/]");
        lines.Add($"  [green]Win:[/]  {FormatPercentage(odds.WinProbability)}");
        lines.Add($"  [yellow]Tie:[/]  {FormatPercentage(odds.TieProbability)}");
        lines.Add($"  [red]Lose:[/] {FormatPercentage(odds.LoseProbability)}");
        lines.Add($"  [cyan]Expected Pot Share:[/] {FormatPercentage(odds.ExpectedPotShare)}");
        lines.Add("");

        // Hand type probabilities section
        lines.Add("[bold yellow]Hand Probabilities[/]");
        
        // Order hand types from best to worst
        var orderedHandTypes = new[]
        {
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
                var barLength = (int)(probability * 20); // Max 20 chars for bar
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
