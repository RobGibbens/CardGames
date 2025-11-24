using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Dealers;
using CardGames.Poker.CLI.Output;
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
        DisplayStudHands(playerNames, playerHoleCards, playerBoardCards);

        // Fourth Street: Deal 1 board card to each player
        Logger.Paragraph("Fourth Street");
        foreach (var name in playerNames)
        {
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayStudHands(playerNames, playerHoleCards, playerBoardCards);

        // Fifth Street: Deal 1 board card to each player
        Logger.Paragraph("Fifth Street");
        foreach (var name in playerNames)
        {
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayStudHands(playerNames, playerHoleCards, playerBoardCards);

        // Sixth Street: Deal 1 board card to each player
        Logger.Paragraph("Sixth Street");
        foreach (var name in playerNames)
        {
            playerBoardCards[name].Add(dealer.DealCard());
        }
        DisplayStudHands(playerNames, playerHoleCards, playerBoardCards);

        // Seventh Street (River): Deal 1 hole card to each player (face down)
        Logger.Paragraph("Seventh Street (final down card)");
        foreach (var name in playerNames)
        {
            playerHoleCards[name].Add(dealer.DealCard());
        }
        DisplayStudHands(playerNames, playerHoleCards, playerBoardCards);

        // Show final hands
        Logger.Paragraph("Final Hands (all cards)");
        foreach (var name in playerNames)
        {
            AnsiConsole.MarkupLine($"[cyan]{name}[/]: [yellow]{playerHoleCards[name].ToStringRepresentation()}[/] (hole) + [green]{playerBoardCards[name].ToStringRepresentation()}[/] (board)");
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
