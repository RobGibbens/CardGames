using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Betting;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Games.SevenCardStud;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Play;

internal class SevenCardStudPlayCommand : Command<SevenCardStudPlaySettings>
{
    private static readonly SpectreLogger Logger = new();
    private const int MinPlayers = 2;
    private const int MaxPlayers = 8;

    protected override int Execute(CommandContext context, SevenCardStudPlaySettings settings, CancellationToken cancellationToken)
    {
        Logger.LogApplicationStart();

        var numberOfPlayers = settings.NumberOfPlayers == default
            ? AnsiConsole.Ask<int>($"How many players? ({MinPlayers}-{MaxPlayers}): ")
            : settings.NumberOfPlayers;

        if (numberOfPlayers < MinPlayers || numberOfPlayers > MaxPlayers)
        {
            AnsiConsole.MarkupLine($"[red]Invalid number of players. Must be between {MinPlayers} and {MaxPlayers}.[/]");
            return 1;
        }

        var startingChips = settings.StartingChips == default
            ? AnsiConsole.Ask<int>("Starting chips per player: ", 1000)
            : settings.StartingChips;

        var ante = settings.Ante == default
            ? AnsiConsole.Ask<int>("Ante amount: ", 5)
            : settings.Ante;

        var bringIn = settings.BringIn == default
            ? AnsiConsole.Ask<int>("Bring-in amount: ", 5)
            : settings.BringIn;

        var smallBet = settings.SmallBet == default
            ? AnsiConsole.Ask<int>("Small bet (3rd/4th street): ", 10)
            : settings.SmallBet;

        var bigBet = settings.BigBet == default
            ? AnsiConsole.Ask<int>("Big bet (5th/6th/7th street): ", 20)
            : settings.BigBet;

        var playerNames = GetPlayerNames(numberOfPlayers);
        var players = playerNames.Select(name => (name, startingChips)).ToList();

        var game = new SevenCardStudGame(players, ante, bringIn, smallBet, bigBet);

        Logger.Paragraph("Seven Card Stud - Game Started");
        AnsiConsole.MarkupLine($"[dim]Ante: {ante} | Bring-in: {bringIn} | Small Bet: {smallBet} | Big Bet: {bigBet}[/]");

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

    private static void PlayHand(SevenCardStudGame game)
    {
        Logger.Paragraph("New Hand");
        DisplayPlayerStacks(game);

        // Start hand
        game.StartHand();

        // Collect antes
        AnsiConsole.MarkupLine("[yellow]Collecting antes...[/]");
        var anteActions = game.CollectAntes();
        foreach (var action in anteActions)
        {
            AnsiConsole.MarkupLine($"[dim]{action}[/]");
        }
        AnsiConsole.MarkupLine($"[green]Pot: {game.TotalPot}[/]");

        // Deal third street
        Logger.Paragraph("Third Street (2 down, 1 up)");
        game.DealThirdStreet();
        DisplayAllHands(game, showHoleCards: true);

        // Post bring-in
        var bringInPlayer = game.GetBringInPlayer();
        if (bringInPlayer != null)
        {
            AnsiConsole.MarkupLine($"[yellow]{bringInPlayer.Player.Name} has the low card and must post the bring-in[/]");
            var bringInAction = game.PostBringIn();
            AnsiConsole.MarkupLine($"[blue]{bringInAction}[/]");
        }

        // Third street betting
        game.StartBettingRound();
        if (!RunBettingRound(game, "Third Street Betting"))
        {
            var result = game.PerformShowdown();
            DisplayShowdownResult(result, game);
            return;
        }

        // Fourth through Seventh streets
        var streets = new[]
        {
            (Phases.FourthStreet, "Fourth Street"),
            (Phases.FifthStreet, "Fifth Street"),
            (Phases.SixthStreet, "Sixth Street"),
            (Phases.SeventhStreet, "Seventh Street (River)")
        };

        foreach (var (phase, streetName) in streets)
        {
            if (game.CurrentPhase != phase)
            {
                break;
            }

            Logger.Paragraph(streetName);
            game.DealStreetCard();
            DisplayAllHands(game, showHoleCards: phase == Phases.SeventhStreet);

            game.StartBettingRound();
            if (!RunBettingRound(game, $"{streetName} Betting"))
            {
                var result = game.PerformShowdown();
                DisplayShowdownResult(result, game);
                return;
            }
        }

        // Showdown
        if (game.CurrentPhase == Phases.Showdown)
        {
            var result = game.PerformShowdown();
            DisplayShowdownResult(result, game);
        }
    }

    private static bool RunBettingRound(SevenCardStudGame game, string roundName)
    {
        var minBet = game.GetCurrentMinBet();

        while (!game.CurrentBettingRound.IsComplete)
        {
            var currentPlayer = game.GetCurrentPlayer();
            var available = game.GetAvailableActions();

            // Clear screen and show fresh game state for current player
            Logger.ClearScreen();
            Logger.Paragraph(roundName);
            AnsiConsole.MarkupLine($"[dim]Bet size this street: {minBet}[/]");

            // Show all players' board cards (visible to everyone)
            DisplayAllBoardCards(game, currentPlayer);
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[green]Pot: {game.TotalPot}[/] | [yellow]Current Bet: {game.CurrentBettingRound.CurrentBet}[/]");
            DisplayPlayerStatus(game, currentPlayer);

            // Show current player's cards (with hole cards visible)
            var gamePlayer = game.GamePlayers.First(gp => gp.Player.Name == currentPlayer.Name);
            AnsiConsole.MarkupLine($"[cyan]{currentPlayer.Name}[/]'s cards:");
            DisplayPlayerCards(gamePlayer, showHoleCards: true);
            AnsiConsole.WriteLine();

            // Show live odds for the current player
            var deadCards = game.GamePlayers
                .Where(gp => gp.Player.HasFolded)
                .SelectMany(gp => gp.HoleCards.Concat(gp.BoardCards))
                .ToList();
            LiveOddsRenderer.RenderStudOdds(
                gamePlayer.HoleCards.ToList(),
                gamePlayer.BoardCards.ToList(),
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

    private static void DisplayShowdownResult(SevenCardStudShowdownResult result, SevenCardStudGame game)
    {
        Logger.Paragraph("Showdown");

        if (result.WonByFold)
        {
            var winner = result.Payouts.First();
            AnsiConsole.MarkupLine($"[bold green]{winner.Key}[/] wins {winner.Value} chips (all others folded)!");
            return;
        }

        // Show all hands
        if (result.PlayerHands != null)
        {
            foreach (var (playerName, (hand, cards)) in result.PlayerHands)
            {
                var handDescription = hand != null ? HandDescriptionFormatter.GetHandDescription(hand) : "Unknown";
                AnsiConsole.MarkupLine($"[cyan]{playerName}[/]:");
                CardRenderer.RenderCards(cards);
                AnsiConsole.MarkupLine($"[magenta]{handDescription}[/]");
                AnsiConsole.WriteLine();
            }
        }

        // Show winners
        Logger.Paragraph("Winners");
        foreach (var (playerName, amount) in result.Payouts)
        {
            AnsiConsole.MarkupLine($"[bold green]{playerName}[/] wins {amount} chips!");
        }
    }

    private static void DisplayPlayerStacks(SevenCardStudGame game)
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

    private static void DisplayPlayerStatus(SevenCardStudGame game, PokerPlayer currentPlayer)
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

    private static void DisplayAllHands(SevenCardStudGame game, bool showHoleCards)
    {
        foreach (var gamePlayer in game.GamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                AnsiConsole.MarkupLine($"[cyan bold]{gamePlayer.Player.Name}[/]:");
                DisplayPlayerCards(gamePlayer, showHoleCards);
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void DisplayPlayerCards(SevenCardStudGamePlayer gamePlayer, bool showHoleCards)
    {
        var boardCards = gamePlayer.BoardCards.ToList();
        var holeCount = gamePlayer.HoleCards.Count;

        if (showHoleCards)
        {
            // Show all cards - hole cards first, then board cards
            var allCards = gamePlayer.HoleCards.Concat(boardCards).ToList();
            CardRenderer.RenderCards(allCards);
            
            var holeDisplay = gamePlayer.HoleCards.ToStringRepresentation();
            var boardDisplay = boardCards.ToStringRepresentation();
            AnsiConsole.MarkupLine($"[dim](hole: {holeDisplay}) (board: {boardDisplay})[/]");
        }
        else
        {
            // Show face-down markers for hole cards, then board cards
            CardRenderer.RenderMixedCards(boardCards, holeCount);
            var boardDisplay = boardCards.ToStringRepresentation();
            AnsiConsole.MarkupLine($"[dim](board: {boardDisplay})[/]");
        }
    }

    /// <summary>
    /// Displays all players' board (face-up) cards visible on the table.
    /// </summary>
    private static void DisplayAllBoardCards(SevenCardStudGame game, PokerPlayer currentPlayer)
    {
        AnsiConsole.MarkupLine("[dim]Board cards visible on table:[/]");
        foreach (var gamePlayer in game.GamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                var boardCards = gamePlayer.BoardCards.ToList();
                var marker = gamePlayer.Player.Name == currentPlayer.Name ? "►" : " ";
                var boardDisplay = boardCards.ToStringRepresentation();
                AnsiConsole.MarkupLine($"[dim]{marker} {gamePlayer.Player.Name}: {boardDisplay}[/]");
            }
        }
    }

    private static void DisplayFinalStandings(SevenCardStudGame game)
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
