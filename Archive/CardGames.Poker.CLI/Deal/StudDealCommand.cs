using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Dealers;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Hands.StudHands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Deal;

internal class StudDealCommand : Command<DealSettings>
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

        var playerNames = DealUtilities.GetPlayerNames(numberOfPlayers);
        DealStudHand(playerNames);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
            DealStudHand(playerNames);
        }

        return 0;
    }

    private static void DealStudHand(List<string> playerNames)
    {
        Logger.Paragraph("Dealing 7-Card Stud Hand");
        
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        // In 7-card stud: 2 down cards, 1 up card, then 3 more up cards, then 1 final down card
        var playerHoleCards = new Dictionary<string, List<Card>>(); // Hidden cards
        var playerBoardCards = new Dictionary<string, List<Card>>(); // Visible cards

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
        DisplayStudHandsWithAscii(playerNames, playerHoleCards, playerBoardCards, animateLastBoard: true);

        // Fourth Street: Deal 1 board card to each player
        Logger.Paragraph("Fourth Street");
        foreach (var name in playerNames)
        {
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayStudHandsWithAscii(playerNames, playerHoleCards, playerBoardCards, animateLastBoard: true);

        // Fifth Street: Deal 1 board card to each player
        Logger.Paragraph("Fifth Street");
        foreach (var name in playerNames)
        {
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayStudHandsWithAscii(playerNames, playerHoleCards, playerBoardCards, animateLastBoard: true);

        // Sixth Street: Deal 1 board card to each player
        Logger.Paragraph("Sixth Street");
        foreach (var name in playerNames)
        {
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayStudHandsWithAscii(playerNames, playerHoleCards, playerBoardCards, animateLastBoard: true);

        // Seventh Street (River): Deal 1 hole card to each player (face down)
        Logger.Paragraph("Seventh Street (final down card)");
        foreach (var name in playerNames)
        {
            playerHoleCards[name].Add(dealer.DealCard());
        }
        DisplayStudHandsWithAscii(playerNames, playerHoleCards, playerBoardCards, animateLastBoard: false);

        // Evaluate hands and display results
        Logger.Paragraph("Hand Evaluation");
        var evaluatedHands = new Dictionary<string, SevenCardStudHand>();
        foreach (var name in playerNames)
        {
            var holeCards = playerHoleCards[name].Take(2).ToList();
            var boardCards = playerBoardCards[name];
            var downCard = playerHoleCards[name].Last();
            
            var hand = new SevenCardStudHand(holeCards, boardCards, downCard);
            evaluatedHands[name] = hand;
            var description = HandDescriptionFormatter.GetHandDescription(hand);
            
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            // Show all cards for the player (hole + board)
            var allCards = playerHoleCards[name].Concat(boardCards).ToList();
            CardRenderer.RenderCards(allCards);
            AnsiConsole.MarkupLine($"[magenta]{description}[/]");
            AnsiConsole.WriteLine();
        }

        // Determine and display winner(s)
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

    private static void DisplayStudHandsWithAscii(
        List<string> playerNames, 
        Dictionary<string, List<Card>> holeCards, 
        Dictionary<string, List<Card>> boardCards,
        bool animateLastBoard)
    {
        foreach (var name in playerNames)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            
            // Show hole cards as face down + board cards as face up
            var board = boardCards[name];
            CardRenderer.RenderMixedCards(board, holeCards[name].Count);
            
            var holeDisplay = holeCards[name].ToStringRepresentation();
            var boardDisplay = board.ToStringRepresentation();
            AnsiConsole.MarkupLine($"[dim](hole: {holeDisplay}) (board: {boardDisplay})[/]");
            AnsiConsole.WriteLine();
        }
    }

    private static void DisplayStudHands(
        List<string> playerNames, 
        Dictionary<string, List<Card>> holeCards, 
        Dictionary<string, List<Card>> boardCards)
    {
        foreach (var name in playerNames)
        {
            var holeDisplay = holeCards[name].ToStringRepresentation();
            var boardDisplay = boardCards[name].ToStringRepresentation();
            AnsiConsole.MarkupLine($"[cyan]{name}[/]: [dim](hole)[/] [yellow]{holeDisplay}[/] [dim](board)[/] [green]{boardDisplay}[/]");
        }
    }
}
