using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Dealers;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Hands.DrawHands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Deal;

internal class TwosJacksManWithTheAxeDealCommand : Command<DealSettings>
{
    private static readonly SpectreLogger Logger = new();

    protected override int Execute(CommandContext context, DealSettings settings, CancellationToken cancellationToken)
    {
        Logger.LogApplicationStart();
        
        var numberOfPlayers = settings.NumberOfPlayers == default
            ? AnsiConsole.Ask<int>("How many players? (2-6): ")
            : settings.NumberOfPlayers;

        if (numberOfPlayers < 2 || numberOfPlayers > 6)
        {
            AnsiConsole.MarkupLine("[red]Invalid number of players. Must be between 2 and 6.[/]");
            return 1;
        }

        var playerNames = DealUtilities.GetPlayerNames(numberOfPlayers);
		DealTwosJacksManWithTheAxeHand(playerNames);

        while (AnsiConsole.Confirm("Deal another hand?"))
        {
			DealTwosJacksManWithTheAxeHand(playerNames);
        }

        return 0;
    }

    private static void DealTwosJacksManWithTheAxeHand(List<string> playerNames)
    {
        Logger.Paragraph("Dealing Twos, Jacks, Man with the Axe Hand");
        
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        // Deal 5 cards to each player
        var playerHands = new Dictionary<string, IReadOnlyCollection<Card>>();
        foreach (var name in playerNames)
        {
            playerHands[name] = dealer.DealCards(5);
        }

        // Display cards with ASCII art and flip animation
        Logger.Paragraph("Dealt Cards");
        foreach (var (name, cards) in playerHands)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{name}[/]:");
            CardAnimator.AnimateCardFlip(cards);
            AnsiConsole.MarkupLine($"[dim]({cards.ToStringRepresentation()})[/]");
            AnsiConsole.WriteLine();
        }

        // Evaluate hands and display results
        Logger.Paragraph("Hand Evaluation");
        var evaluatedHands = new Dictionary<string, DrawHand>();
        foreach (var (name, cards) in playerHands)
        {
            var hand = new DrawHand(cards.ToList());
            evaluatedHands[name] = hand;
            var description = HandDescriptionFormatter.GetHandDescription(hand);
            AnsiConsole.MarkupLine($"[cyan]{name}[/]:");
            CardRenderer.RenderCards(cards);
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
}
