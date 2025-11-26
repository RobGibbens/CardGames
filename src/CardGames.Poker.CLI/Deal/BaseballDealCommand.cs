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

/// <summary>
/// Deal command for Baseball - a 7-card stud variant where 3s and 9s are wild,
/// and receiving a 4 face up grants an extra card.
/// </summary>
internal class BaseballDealCommand : Command<DealSettings>
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
        DealBaseballHand(playerNames);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
            DealBaseballHand(playerNames);
        }

        return 0;
    }

    private static void DealBaseballHand(List<string> playerNames)
    {
        Logger.Paragraph("Dealing Baseball Hand");
        AnsiConsole.MarkupLine("[dim]Wild cards: 3s and 9s. Fours dealt face up grant extra cards.[/]");
        
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
            var boardCard = dealer.DealCard();
            playerBoardCards[name].Add(boardCard);
            // Check for 4s and deal extra cards
            DealExtraCardsForFour(dealer, boardCard, playerBoardCards[name]);
        }
        DisplayBaseballHands(playerNames, playerHoleCards, playerBoardCards);

        // Fourth through Sixth Street: Deal 1 board card to each player
        for (int street = 4; street <= 6; street++)
        {
            Logger.Paragraph($"{GetStreetName(street)} Street");
            foreach (var name in playerNames)
            {
                var boardCard = dealer.DealCard();
                playerBoardCards[name].Add(boardCard);
                DealExtraCardsForFour(dealer, boardCard, playerBoardCards[name]);
            }
            DisplayBaseballHands(playerNames, playerHoleCards, playerBoardCards);
        }

        // Seventh Street: Deal 1 hole card to each player (face down)
        Logger.Paragraph("Seventh Street (final down card)");
        foreach (var name in playerNames)
        {
            playerHoleCards[name].Add(dealer.DealCard());
        }
        DisplayBaseballHands(playerNames, playerHoleCards, playerBoardCards);

        // Evaluate hands
        EvaluateAndDisplayWinner(playerNames, playerHoleCards, playerBoardCards);
    }

    private static void DealExtraCardsForFour(FrenchDeckDealer dealer, Card card, List<Card> boardCards)
    {
        if (card.Symbol != Symbol.Four)
        {
            return;
        }
        
        AnsiConsole.MarkupLine("[yellow]  → 4 dealt face up! Extra card granted.[/]");
        var extraCard = dealer.DealCard();
        boardCards.Add(extraCard);
        
        // Chain: if the extra card is also a 4, deal another extra card
        while (extraCard.Symbol == Symbol.Four)
        {
            AnsiConsole.MarkupLine("[yellow]  → Another 4! Extra card granted.[/]");
            extraCard = dealer.DealCard();
            boardCards.Add(extraCard);
        }
    }

    private static string GetStreetName(int street) => street switch
    {
        4 => "Fourth",
        5 => "Fifth",
        6 => "Sixth",
        _ => street.ToString()
    };

    private static void DisplayBaseballHands(
        List<string> playerNames, 
        Dictionary<string, List<Card>> holeCards, 
        Dictionary<string, List<Card>> boardCards)
    {
        foreach (var name in playerNames)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            
            var board = boardCards[name];
            
            // Determine wild cards (3s and 9s) for coloring
            var wildInBoard = board.Where(c => c.Symbol == Symbol.Three || c.Symbol == Symbol.Nine).ToList();
            
            CardRenderer.RenderMixedCards(board, holeCards[name].Count, wildCards: wildInBoard);
            
            var holeDisplay = holeCards[name].ToStringRepresentation();
            var boardDisplay = board.ToStringRepresentation();
            
            // Highlight wild cards (3s and 9s)
            var wildInHole = holeCards[name].Where(c => c.Symbol == Symbol.Three || c.Symbol == Symbol.Nine).ToList();
            
            AnsiConsole.MarkupLine($"[dim](hole: {holeDisplay}) (board: {boardDisplay})[/]");
            if (wildInHole.Count + wildInBoard.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]  Wild cards: {wildInHole.Concat(wildInBoard).ToStringRepresentation()}[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    private static void EvaluateAndDisplayWinner(
        List<string> playerNames,
        Dictionary<string, List<Card>> holeCards,
        Dictionary<string, List<Card>> boardCards)
    {
        Logger.Paragraph("Hand Evaluation");
        var evaluatedHands = new Dictionary<string, BaseballHand>();
        foreach (var name in playerNames)
        {
            var initialHoleCards = holeCards[name].Take(2).ToList();
            var board = boardCards[name];
            var downCards = holeCards[name].Skip(2).ToList();
            
            var hand = new BaseballHand(initialHoleCards, board, downCards);
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
