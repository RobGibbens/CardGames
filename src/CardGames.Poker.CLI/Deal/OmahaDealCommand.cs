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

internal class OmahaDealCommand : Command<DealSettings>
{
    private static readonly SpectreLogger Logger = new();

    protected override int Execute(CommandContext context, DealSettings settings, CancellationToken cancellationToken)
    {
        Logger.LogApplicationStart();
        
        var numberOfPlayers = settings.NumberOfPlayers == default
            ? AnsiConsole.Ask<int>("How many players? (2-10): ")
            : settings.NumberOfPlayers;

        if (numberOfPlayers < 2 || numberOfPlayers > 10)
        {
            AnsiConsole.MarkupLine("[red]Invalid number of players. Must be between 2 and 10.[/]");
            return 1;
        }

        var playerNames = GetPlayerNames(numberOfPlayers);
        DealOmahaHand(playerNames);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
            DealOmahaHand(playerNames);
        }

        return 0;
    }

    private static List<string> GetPlayerNames(int numberOfPlayers)
    {
        var names = new List<string>();
        for (int i = 1; i <= numberOfPlayers; i++)
        {
            var name = AnsiConsole.Ask<string>($"Player {i} name: ");
            names.Add(name);
        }
        return names;
    }

    private static void DealOmahaHand(List<string> playerNames)
    {
        Logger.Paragraph("Dealing Omaha Hand");
        
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        // Deal hole cards to each player (4 cards each in Omaha)
        var playerHands = new Dictionary<string, IReadOnlyCollection<Card>>();
        foreach (var name in playerNames)
        {
            playerHands[name] = dealer.DealCards(4);
        }

        // Display hole cards
        Logger.Paragraph("Hole Cards");
        foreach (var (name, cards) in playerHands)
        {
            AnsiConsole.MarkupLine($"[cyan]{name}[/]: [yellow]{cards.ToStringRepresentation()}[/]");
        }

        // Deal flop (burn 1, deal 3)
        dealer.DealCard(); // burn
        var flop = dealer.DealCards(3);
        Logger.Paragraph("Flop");
        AnsiConsole.MarkupLine($"[green]{flop.ToStringRepresentation()}[/]");

        // Deal turn (burn 1, deal 1)
        dealer.DealCard(); // burn
        var turn = dealer.DealCard();
        Logger.Paragraph("Turn");
        AnsiConsole.MarkupLine($"[green]{flop.ToStringRepresentation()} {turn}[/]");

        // Deal river (burn 1, deal 1)
        dealer.DealCard(); // burn
        var river = dealer.DealCard();
        Logger.Paragraph("River");
        var communityCards = flop.Concat(new[] { turn, river }).ToList();
        AnsiConsole.MarkupLine($"[green]{communityCards.ToStringRepresentation()}[/]");

        // Show final hands
        Logger.Paragraph("Final Hands");
        foreach (var (name, holeCards) in playerHands)
        {
            AnsiConsole.MarkupLine($"[cyan]{name}[/]: [yellow]{holeCards.ToStringRepresentation()}[/] + [green]{communityCards.ToStringRepresentation()}[/]");
        }
    }
}
