using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Dealers;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Hands.CommunityCardHands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Deal;

internal class HoldemDealCommand : Command<DealSettings>
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

        var playerNames = DealUtilities.GetPlayerNames(numberOfPlayers);
        DealHoldemHand(playerNames);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
            DealHoldemHand(playerNames);
        }

        return 0;
    }

    private static void DealHoldemHand(List<string> playerNames)
    {
        Logger.Paragraph("Dealing Holdem Hand");
        
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        // Deal hole cards to each player (2 cards each)
        var playerHands = new Dictionary<string, IReadOnlyCollection<Card>>();
        foreach (var name in playerNames)
        {
            playerHands[name] = dealer.DealCards(2);
        }

        // Display hole cards with ASCII art and flip animation
        Logger.Paragraph("Hole Cards");
        foreach (var (name, cards) in playerHands)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            CardAnimator.AnimateCardFlip(cards);
            AnsiConsole.MarkupLine($"[dim]({cards.ToStringRepresentation()})[/]");
            AnsiConsole.WriteLine();
        }

        // Deal flop (burn 1, deal 3)
        dealer.DealCard(); // burn
        var flop = dealer.DealCards(3);
        Logger.Paragraph("Flop");
        CardAnimator.AnimateCardFlip(flop);
        AnsiConsole.MarkupLine($"[dim]({flop.ToStringRepresentation()})[/]");

        // Deal turn (burn 1, deal 1)
        dealer.DealCard(); // burn
        var turn = dealer.DealCard();
        Logger.Paragraph("Turn");
        var flopAndTurn = flop.Concat(new[] { turn }).ToList();
        CardRenderer.RenderCards(flopAndTurn);
        AnsiConsole.MarkupLine($"[dim]({flopAndTurn.ToStringRepresentation()})[/]");

        // Deal river (burn 1, deal 1)
        dealer.DealCard(); // burn
        var river = dealer.DealCard();
        Logger.Paragraph("River");
        var communityCards = flop.Concat(new[] { turn, river }).ToList();
        CardRenderer.RenderCards(communityCards);
        AnsiConsole.MarkupLine($"[dim]({communityCards.ToStringRepresentation()})[/]");

        // Evaluate hands and display results
        Logger.Paragraph("Hand Evaluation");
        var evaluatedHands = new Dictionary<string, HoldemHand>();
        foreach (var (name, holeCards) in playerHands)
        {
            var hand = new HoldemHand(holeCards, communityCards);
            evaluatedHands[name] = hand;
            var description = HandDescriptionFormatter.GetHandDescription(hand);
            AnsiConsole.MarkupLine($"[cyan]{name}[/]:");
            CardRenderer.RenderCards(holeCards);
            AnsiConsole.MarkupLine($"[magenta]{description}[/]");
            AnsiConsole.WriteLine();
        }

        // Determine and display winner
        var winner = evaluatedHands.MaxBy(kvp => kvp.Value.Strength);
        var winningDescription = HandDescriptionFormatter.GetHandDescription(winner.Value);
        
        Logger.Paragraph("Winner");
        AnsiConsole.MarkupLine($"[bold green]{winner.Key}[/] wins with [bold magenta]{winningDescription}[/]!");
    }
}
