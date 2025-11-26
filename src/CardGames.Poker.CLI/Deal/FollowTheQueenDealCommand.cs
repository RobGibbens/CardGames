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
/// Deal command for Follow the Queen - a 7-card stud variant where Queens are 
/// always wild, and the card following the last face-up Queen also becomes wild
/// (along with all cards of that rank).
/// </summary>
internal class FollowTheQueenDealCommand : Command<DealSettings>
{
    private static readonly SpectreLogger Logger = new();
    private static readonly FollowTheQueenWildCardRules WildCardRules = new();

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
        DealFollowTheQueenHand(playerNames);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
            DealFollowTheQueenHand(playerNames);
        }

        return 0;
    }

    private static void DealFollowTheQueenHand(List<string> playerNames)
    {
        Logger.Paragraph("Dealing Follow the Queen Hand");
        AnsiConsole.MarkupLine("[dim]Queens are wild. The card following the last face-up Queen is also wild.[/]");
        
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        var playerHoleCards = new Dictionary<string, List<Card>>();
        var playerBoardCards = new Dictionary<string, List<Card>>();
        var faceUpCardsInOrder = new List<Card>();

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
            var boardCard = dealer.DealCard();
            playerBoardCards[name].Add(boardCard);
            faceUpCardsInOrder.Add(boardCard);
            
            if (boardCard.Symbol == Symbol.Queen)
            {
                AnsiConsole.MarkupLine($"[yellow]  → {name} got a Queen face up![/]");
            }
        }
        DisplayFollowTheQueenHands(playerNames, playerHoleCards, playerBoardCards, faceUpCardsInOrder);

        // Fourth through Sixth Street
        for (int street = 4; street <= 6; street++)
        {
            Logger.Paragraph($"{GetStreetName(street)} Street");
            foreach (var name in playerNames)
            {
                var boardCard = dealer.DealCard();
                playerBoardCards[name].Add(boardCard);
                faceUpCardsInOrder.Add(boardCard);
                
                if (boardCard.Symbol == Symbol.Queen)
                {
                    AnsiConsole.MarkupLine($"[yellow]  → {name} got a Queen face up![/]");
                }
            }
            DisplayFollowTheQueenHands(playerNames, playerHoleCards, playerBoardCards, faceUpCardsInOrder);
        }

        // Seventh Street: Deal 1 hole card to each player (face down)
        Logger.Paragraph("Seventh Street (final down card)");
        foreach (var name in playerNames)
        {
            playerHoleCards[name].Add(dealer.DealCard());
        }
        DisplayFollowTheQueenHands(playerNames, playerHoleCards, playerBoardCards, faceUpCardsInOrder);

        // Evaluate hands
        EvaluateAndDisplayWinner(playerNames, playerHoleCards, playerBoardCards, faceUpCardsInOrder);
    }

    private static string GetStreetName(int street) => street switch
    {
        4 => "Fourth",
        5 => "Fifth",
        6 => "Sixth",
        _ => street.ToString()
    };

    private static void DisplayFollowTheQueenHands(
        List<string> playerNames, 
        Dictionary<string, List<Card>> holeCards, 
        Dictionary<string, List<Card>> boardCards,
        List<Card> faceUpCardsInOrder)
    {
        // Display current wild rank information
        var followingWildRank = DetermineFollowingWildRank(faceUpCardsInOrder);
        if (followingWildRank.HasValue)
        {
            AnsiConsole.MarkupLine($"[yellow]Current wild ranks: Queens and {followingWildRank.Value}s[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Current wild rank: Queens only[/]");
        }
        
        foreach (var name in playerNames)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            
            var board = boardCards[name];
            CardRenderer.RenderMixedCards(board, holeCards[name].Count);
            
            var holeDisplay = holeCards[name].ToStringRepresentation();
            var boardDisplay = board.ToStringRepresentation();
            
            // Determine wild cards for current hand
            var allCards = holeCards[name].Concat(board).ToList();
            var wildCards = WildCardRules.DetermineWildCards(allCards, faceUpCardsInOrder);
            
            AnsiConsole.MarkupLine($"[dim](hole: {holeDisplay}) (board: {boardDisplay})[/]");
            if (wildCards.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]  Wild cards: {wildCards.ToStringRepresentation()}[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    private static Symbol? DetermineFollowingWildRank(List<Card> faceUpCardsInOrder)
    {
        Symbol? followingRank = null;
        
        for (int i = 0; i < faceUpCardsInOrder.Count - 1; i++)
        {
            if (faceUpCardsInOrder[i].Symbol == Symbol.Queen)
            {
                var nextCard = faceUpCardsInOrder[i + 1];
                if (nextCard.Symbol != Symbol.Queen)
                {
                    followingRank = nextCard.Symbol;
                }
            }
        }
        
        return followingRank;
    }

    private static void EvaluateAndDisplayWinner(
        List<string> playerNames,
        Dictionary<string, List<Card>> holeCards,
        Dictionary<string, List<Card>> boardCards,
        List<Card> faceUpCardsInOrder)
    {
        Logger.Paragraph("Hand Evaluation");
        var evaluatedHands = new Dictionary<string, FollowTheQueenHand>();
        foreach (var name in playerNames)
        {
            var initialHoleCards = holeCards[name].Take(2).ToList();
            var board = boardCards[name];
            var downCard = holeCards[name].Last();
            
            var hand = new FollowTheQueenHand(initialHoleCards, board, downCard, faceUpCardsInOrder);
            evaluatedHands[name] = hand;
            var description = HandDescriptionFormatter.GetHandDescription(hand);
            
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            var allCards = holeCards[name].Concat(board).ToList();
            CardRenderer.RenderCards(allCards);
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
