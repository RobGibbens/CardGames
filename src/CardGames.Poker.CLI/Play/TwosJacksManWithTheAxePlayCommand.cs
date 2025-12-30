using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Betting;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Games.TwosJacksManWithTheAxe;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CardGames.Poker.CLI.Play;

internal class TwosJacksManWithTheAxePlayCommand : Command<PlaySettings>
{
    private static readonly SpectreLogger Logger = new();

    protected override int Execute(CommandContext context, PlaySettings settings, CancellationToken cancellationToken)
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

        var startingChips = settings.StartingChips == default
            ? AnsiConsole.Ask<int>("Starting chips per player: ", 1000)
            : settings.StartingChips;

        var ante = settings.Ante == default
            ? AnsiConsole.Ask<int>("Ante amount: ", 10)
            : settings.Ante;

        var minBet = settings.MinBet == default
            ? AnsiConsole.Ask<int>("Minimum bet: ", 20)
            : settings.MinBet;

        var playerNames = GetPlayerNames(numberOfPlayers);
        var players = playerNames.Select(name => (name, startingChips)).ToList();

        var game = new TwosJacksManWithTheAxeGame(players, ante, minBet);

        Logger.Paragraph("Game Started");
        AnsiConsole.MarkupLine($"[dim]Ante: {ante} | Min Bet: {minBet}[/]");

        do
        {
            PlayHand(game);
        }
        while (game.CanContinue() && AnsiConsole.Confirm("Play another hand?"));

        Logger.Paragraph("Game Over");
        DisplayFinalStandings(game);

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

    private static void PlayHand(TwosJacksManWithTheAxeGame game)
    {
        Logger.Paragraph("New Hand");
        DisplayPlayerStacks(game);

        //TODO:ROB - Call api
		// Start hand
		game.StartHand();

        // Collect antes
        AnsiConsole.MarkupLine("[yellow]Collecting antes...[/]");
        
        //TODO:ROB - Call api
		var anteActions = game.CollectAntes();
        foreach (var action in anteActions)
        {
            AnsiConsole.MarkupLine($"[dim]{action}[/]");
        }
        AnsiConsole.MarkupLine($"[green]Pot: {game.TotalPot}[/]");

        // Deal hands
        AnsiConsole.MarkupLine("[yellow]Dealing cards...[/]");
        game.DealHands();

        // Display hands (in a real game, only show current player's hand)
        DisplayAllHands(game);

        // First betting round
        if (!RunBettingRound(game, "First Betting Round"))
        {
            // Hand ended due to folds
            var result = game.PerformShowdown();
            DisplayShowdownResult(result);
            return;
        }

        // Draw phase
        if (game.CurrentPhase == TwosJacksManWithTheAxePhase.DrawPhase)
        {
            RunDrawPhase(game);
        }

        // Second betting round
        if (game.CurrentPhase == TwosJacksManWithTheAxePhase.SecondBettingRound)
        {
            if (!RunBettingRound(game, "Second Betting Round"))
            {
                var result = game.PerformShowdown();
                DisplayShowdownResult(result);
                return;
            }
        }

        // Showdown
        if (game.CurrentPhase == TwosJacksManWithTheAxePhase.Showdown)
        {
            var result = game.PerformShowdown();
            DisplayShowdownResult(result);
        }
    }

    private static bool RunBettingRound(TwosJacksManWithTheAxeGame game, string roundName)
    {
        while (!game.CurrentBettingRound.IsComplete)
        {
            var currentPlayer = game.GetCurrentPlayer();
            var available = game.GetAvailableActions();

            // Clear screen and show fresh game state for current player
            Logger.ClearScreen();
            Logger.Paragraph(roundName);

            AnsiConsole.MarkupLine($"[green]Pot: {game.TotalPot}[/] | [yellow]Current Bet: {game.CurrentBettingRound.CurrentBet}[/]");
            DisplayPlayerStatus(game, currentPlayer);

            // Show current player's hand
            var gamePlayer = game.GamePlayers.First(gp => gp.Player.Name == currentPlayer.Name);
            AnsiConsole.MarkupLine($"[cyan]{currentPlayer.Name}[/]'s hand:");
            CardRenderer.RenderCards(gamePlayer.Hand);
            AnsiConsole.MarkupLine($"[dim]({gamePlayer.Hand.ToStringRepresentation()})[/]");
            AnsiConsole.WriteLine();

            // Show live odds for the current player
            var deadCards = game.GamePlayers
                .Where(gp => gp.Player.HasFolded)
                .SelectMany(gp => gp.Hand)
                .ToList();
            LiveOddsRenderer.RenderDrawOdds(
                gamePlayer.Hand.ToList(),
                deadCards);
            AnsiConsole.WriteLine();

            var action = PromptForAction(currentPlayer, available);
            var result = game.ProcessBettingAction(action.ActionType, action.Amount);

            if (!result.Success)
            {
                AnsiConsole.MarkupLine($"[red]{result.ErrorMessage}[/]");
                continue;
            }

            AnsiConsole.MarkupLine($"[blue]{result.Action}[/]");

            // Check if only one player remains
            if (game.CurrentBettingRound.PlayersInHand <= 1)
            {
                return false;
            }
        }

        return true;
    }

    private static (BettingActionType ActionType, int Amount) PromptForAction(PokerPlayer player, AvailableActions available)
    {
        var choices = new List<string>();

        if (available.CanCheck) choices.Add("Check");
        if (available.CanBet) choices.Add($"Bet ({available.MinBet}-{available.MaxBet})");
        if (available.CanCall) choices.Add($"Call {available.CallAmount}");
        if (available.CanRaise) choices.Add($"Raise (min {available.MinRaise})");
        if (available.CanFold) choices.Add("Fold");
        if (available.CanAllIn) choices.Add($"All-In ({available.MaxBet})");

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[cyan]{player.Name}[/] ({player.ChipStack} chips) - Your action:")
                .AddChoices(choices));

        if (choice == "Check")
        {
            return (BettingActionType.Check, 0);
        }
        else if (choice.StartsWith("Call"))
        {
            return (BettingActionType.Call, available.CallAmount);
        }
        else if (choice == "Fold")
        {
            return (BettingActionType.Fold, 0);
        }
        else if (choice.StartsWith("All-In"))
        {
            return (BettingActionType.AllIn, available.MaxBet);
        }
        else if (choice.StartsWith("Bet"))
        {
            var amount = AnsiConsole.Ask<int>($"Bet amount ({available.MinBet}-{available.MaxBet}): ");
            return (BettingActionType.Bet, amount);
        }
        else if (choice.StartsWith("Raise"))
        {
            var amount = AnsiConsole.Ask<int>($"Raise to (min {available.MinRaise}): ");
            return (BettingActionType.Raise, amount);
        }

        return (BettingActionType.Fold, 0);
    }

    private static void RunDrawPhase(TwosJacksManWithTheAxeGame game)
    {
        while (game.CurrentPhase == TwosJacksManWithTheAxePhase.DrawPhase)
        {
            var drawPlayer = game.GetCurrentDrawPlayer();
            if (drawPlayer == null)
            {
                game.CompleteDrawPhase();
                break;
            }

            // Clear screen and show fresh game state for draw phase
            Logger.ClearScreen();
            Logger.Paragraph("Draw Phase");

            AnsiConsole.MarkupLine($"[cyan]{drawPlayer.Player.Name}[/]'s turn to draw:");
            CardRenderer.RenderCards(drawPlayer.Hand);

            // Check if player has an Ace to show appropriate hint
            var hasAce = drawPlayer.Hand.Any(c => c.Symbol == Symbol.Ace);
            var maxDiscards = hasAce ? 4 : 3;
            var discardHint = hasAce
                ? $"[dim]You may discard up to {maxDiscards} cards (Ace bonus!)[/]"
                : $"[dim]You may discard up to {maxDiscards} cards[/]";

            // Display cards with indices
            AnsiConsole.MarkupLine("[dim]Card positions: 0, 1, 2, 3, 4[/]");
            AnsiConsole.MarkupLine($"[dim]({drawPlayer.Hand.ToStringRepresentation()})[/]");
            AnsiConsole.MarkupLine(discardHint);

            var discardInput = AnsiConsole.Ask<string>("Enter positions to discard (0-4, space-separated) or press Enter to stand pat: ", "");

            var discardIndices = new List<int>();
            if (!string.IsNullOrWhiteSpace(discardInput))
            {
                var parts = discardInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (int.TryParse(part, out var index))
                    {
                        discardIndices.Add(index);
                    }
                }
            }

            var result = game.ProcessDraw(discardIndices);

            if (!result.Success)
            {
                AnsiConsole.MarkupLine($"[red]{result.ErrorMessage}[/]");
                continue;
            }

            if (discardIndices.Count > 0)
            {
                AnsiConsole.MarkupLine($"[blue]{drawPlayer.Player.Name} discards {discardIndices.Count} card(s) and draws {result.NewCards.Count} card(s)[/]");
                AnsiConsole.MarkupLine($"New hand:");
                CardRenderer.RenderCards(drawPlayer.Hand);
                AnsiConsole.MarkupLine($"[dim]({drawPlayer.Hand.ToStringRepresentation()})[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[blue]{drawPlayer.Player.Name} stands pat[/]");
            }
        }
    }

    private static void DisplayShowdownResult(ShowdownResult result)
    {
        Logger.Paragraph("Showdown");

        if (result.WonByFold)
        {
            var winner = result.Payouts.First();
            AnsiConsole.MarkupLine($"[bold green]{winner.Key}[/] wins {winner.Value} chips (all others folded)!");
            return;
        }

        // Show all hands
        foreach (var (playerName, (hand, cards)) in result.PlayerHands)
        {
            var handDescription = hand != null ? HandDescriptionFormatter.GetHandDescription(hand) : "Unknown";
            AnsiConsole.MarkupLine($"[cyan]{playerName}[/]:");
            CardRenderer.RenderCards(cards);
            AnsiConsole.MarkupLine($"[magenta]{handDescription}[/]");
            AnsiConsole.WriteLine();
        }

        // Show winners
        Logger.Paragraph("Winners");
        foreach (var (playerName, amount) in result.Payouts)
        {
            AnsiConsole.MarkupLine($"[bold green]{playerName}[/] wins {amount} chips!");
        }
    }

    private static void DisplayPlayerStacks(TwosJacksManWithTheAxeGame game)
    {
        var table = new Table();
        table.AddColumn("Player");
        table.AddColumn("Chips");
        table.AddColumn("Status");

        foreach (var gamePlayer in game.GamePlayers)
        {
            var player = gamePlayer.Player;
            var status = player.HasFolded ? "[red]Folded[/]" :
                        player.IsAllIn ? "[yellow]All-In[/]" :
                        "[green]Active[/]";
            table.AddRow(player.Name, player.ChipStack.ToString(), status);
        }

        AnsiConsole.Write(table);
    }

    private static void DisplayPlayerStatus(TwosJacksManWithTheAxeGame game, PokerPlayer currentPlayer)
    {
        var playersInfo = game.GamePlayers.Select(gp =>
        {
            var p = gp.Player;
            var marker = p.Name == currentPlayer.Name ? "►" : " ";
            var status = p.HasFolded ? "(folded)" :
                        p.IsAllIn ? "(all-in)" : "";
            var bet = p.CurrentBet > 0 ? $"bet: {p.CurrentBet}" : "";
            return $"{marker} {p.Name}: {p.ChipStack} chips {bet} {status}";
        });

        AnsiConsole.MarkupLine($"[dim]{string.Join(" | ", playersInfo)}[/]");
    }

    private static void DisplayAllHands(TwosJacksManWithTheAxeGame game)
    {
        foreach (var gamePlayer in game.GamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                AnsiConsole.MarkupLine($"[cyan bold]{gamePlayer.Player.Name}[/]'s hand:");
                CardRenderer.RenderCards(gamePlayer.Hand);
                AnsiConsole.MarkupLine($"[dim]({gamePlayer.Hand.ToStringRepresentation()})[/]");
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void DisplayFinalStandings(TwosJacksManWithTheAxeGame game)
    {
        var standings = game.GamePlayers
            .OrderByDescending(gp => gp.Player.ChipStack)
            .Select((gp, index) => new { Rank = index + 1, gp.Player.Name, Chips = gp.Player.ChipStack })
            .ToList();

        var table = new Table();
        table.AddColumn("Rank");
        table.AddColumn("Player");
        table.AddColumn("Chips");

        foreach (var standing in standings)
        {
            table.AddRow(standing.Rank.ToString(), standing.Name, standing.Chips.ToString());
        }

        AnsiConsole.Write(table);
    }
}
