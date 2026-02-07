using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Dealers;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Deal;

/// <summary>
/// Deal command for Kings and Lows - a 7-card stud variant where your lowest 
/// card (and all cards of that rank) are wild. Optionally requires a King to 
/// use the wild card benefit.
/// </summary>
internal class KingsAndLowsDealCommand : Command<DealSettings>
{
    private static readonly SpectreLogger Logger = new();

    protected override int Execute(CommandContext context, DealSettings settings, CancellationToken cancellationToken)
    {
        Logger.LogApplicationStart();
        
        var numberOfPlayers = settings.NumberOfPlayers == default
            ? AnsiConsole.Ask<int>("How many players? (2-8): ")
            : settings.NumberOfPlayers;

        if (numberOfPlayers < 2 || numberOfPlayers > 8)
        {
            AnsiConsole.MarkupLine("[red]Invalid number of players. Must be between 2 and 8.[/]");
            return 1;
        }

        var kingRequired = AnsiConsole.Confirm("Require King to use low card as wild?", false);
        var wildCardRules = new WildCardRules(kingRequired);

        var playerNames = DealUtilities.GetPlayerNames(numberOfPlayers);
        DealKingsAndLowsHand(playerNames, wildCardRules, kingRequired);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
            DealKingsAndLowsHand(playerNames, wildCardRules, kingRequired);
        }

        return 0;
    }

    private static void DealKingsAndLowsHand(List<string> playerNames, WildCardRules wildCardRules, bool kingRequired)
    {
        Logger.Paragraph("Dealing Kings and Lows Hand");
        if (kingRequired)
        {
            AnsiConsole.MarkupLine("[dim]Your lowest card is wild only if you have a King.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Your lowest card (and all cards of that rank) are wild.[/]");
        }
        
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        var playerHoleCards = new Dictionary<string, List<Card>>();
        var playerBoardCards = new Dictionary<string, List<Card>>();

        foreach (var name in playerNames)
        {
            playerHoleCards[name] = new List<Card>();
            playerBoardCards[name] = new List<Card>();
        }

        // Third Street: Deal 2 hole cards and 1 board card to each player
        Logger.Paragraph("Third Street (2 down, 1 up)");
        foreach (var name in playerNames)
        {
            playerHoleCards[name].AddRange(dealer.DealCards(2));
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayKingsAndLowsHands(playerNames, playerHoleCards, playerBoardCards, wildCardRules);

        // Fourth through Sixth Street
        for (int street = 4; street <= 6; street++)
        {
            Logger.Paragraph($"{GetStreetName(street)} Street");
            foreach (var name in playerNames)
            {
                playerBoardCards[name].Add(dealer.DealCard());
            }
            DisplayKingsAndLowsHands(playerNames, playerHoleCards, playerBoardCards, wildCardRules);
        }

        // Seventh Street: Deal 1 hole card to each player (face down)
        Logger.Paragraph("Seventh Street (final down card)");
        foreach (var name in playerNames)
        {
            playerHoleCards[name].Add(dealer.DealCard());
        }
        DisplayKingsAndLowsHands(playerNames, playerHoleCards, playerBoardCards, wildCardRules);

        // Evaluate hands
        EvaluateAndDisplayWinner(playerNames, playerHoleCards, playerBoardCards, wildCardRules);
    }

    private static string GetStreetName(int street) => street switch
    {
        4 => "Fourth",
        5 => "Fifth",
        6 => "Sixth",
        _ => street.ToString()
    };

    private static void DisplayKingsAndLowsHands(
        List<string> playerNames, 
        Dictionary<string, List<Card>> holeCards, 
        Dictionary<string, List<Card>> boardCards,
        WildCardRules wildCardRules)
    {
        foreach (var name in playerNames)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            
            var board = boardCards[name];
            
            // Determine wild cards for current hand (for coloring visible board cards)
            var allCards = holeCards[name].Concat(board).ToList();
            var wildCards = wildCardRules.DetermineWildCards(allCards);
            var wildInBoard = board.Where(c => wildCards.Contains(c)).ToList();
            
            CardRenderer.RenderMixedCards(board, holeCards[name].Count, wildCards: wildInBoard);
            
            var holeDisplay = holeCards[name].ToStringRepresentation();
            var boardDisplay = board.ToStringRepresentation();
            
            AnsiConsole.MarkupLine($"[dim](hole: {holeDisplay}) (board: {boardDisplay})[/]");
            if (wildCards.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]  Wild cards: {wildCards.ToStringRepresentation()}[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    private static void EvaluateAndDisplayWinner(
        List<string> playerNames,
        Dictionary<string, List<Card>> holeCards,
        Dictionary<string, List<Card>> boardCards,
        WildCardRules wildCardRules)
    {
        Logger.Paragraph("Hand Evaluation");
        var evaluatedHands = new Dictionary<string, KingsAndLowsHand>();
        foreach (var name in playerNames)
        {
            var initialHoleCards = holeCards[name].Take(2).ToList();
            var board = boardCards[name];
            var downCard = holeCards[name].Last();
            
            var hand = new KingsAndLowsHand(initialHoleCards, board, downCard, wildCardRules);
            evaluatedHands[name] = hand;
            var description = HandDescriptionFormatter.GetHandDescription(hand);
            
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            var allCards = holeCards[name].Concat(board).ToList();
            CardRenderer.RenderCards(allCards, wildCards: hand.WildCards);
            AnsiConsole.MarkupLine($"[magenta]{description}[/]");
            if (hand.WildCards.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]Wild cards used: {hand.WildCards.ToStringRepresentation()}[/]");
            }
            AnsiConsole.WriteLine();
        }

        var maxStrength = evaluatedHands.Max(kvp => kvp.Value.Strength);
        var winners = evaluatedHands.Where(kvp => kvp.Value.Strength == maxStrength).ToList();
        
        if (winners.Count > 1)
        {
            var winningDescription = HandDescriptionFormatter.GetHandDescription(winners.First().Value);
            Logger.Paragraph("Tie");
            var winnerNames = winners.Count == 2
                ? $"{winners[0].Key} and {winners[1].Key}"
                : string.Join(", ", winners.Take(winners.Count - 1).Select(w => w.Key)) + $", and {winners.Last().Key}";
            AnsiConsole.MarkupLine($"[bold yellow]{winnerNames}[/] tie with [bold magenta]{winningDescription}[/]!");
        }
        else
        {
            var winner = winners.First();
            var winningDescription = HandDescriptionFormatter.GetHandDescription(winner.Value);
            Logger.Paragraph("Winner");
            AnsiConsole.MarkupLine($"[bold green]{winner.Key}[/] wins with [bold magenta]{winningDescription}[/]!");
        }
    }
}
