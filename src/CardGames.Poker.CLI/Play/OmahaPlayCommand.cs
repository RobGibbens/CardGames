using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Betting;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Games;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Play;

internal class OmahaPlayCommand : Command<OmahaPlaySettings>
{
    private static readonly SpectreLogger Logger = new();
    private const int MinPlayers = 2;
    private const int MaxPlayers = 10;

    protected override int Execute(CommandContext context, OmahaPlaySettings settings, CancellationToken cancellationToken)
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

        var smallBlind = settings.SmallBlind == default
            ? AnsiConsole.Ask<int>("Small blind amount: ", 5)
            : settings.SmallBlind;

        var bigBlind = settings.BigBlind == default
            ? AnsiConsole.Ask<int>("Big blind amount: ", 10)
            : settings.BigBlind;

        var playerNames = GetPlayerNames(numberOfPlayers);
        var players = playerNames.Select(name => (name, startingChips)).ToList();

        var game = new OmahaGame(players, smallBlind, bigBlind);

        Logger.Paragraph("Omaha - Game Started");
        AnsiConsole.MarkupLine($"[dim]Small Blind: {smallBlind} | Big Blind: {bigBlind}[/]");

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

    private static void PlayHand(OmahaGame game)
    {
        Logger.Paragraph("New Hand");
        DisplayPlayerStacks(game);

        // Start hand
        game.StartHand();

        // Show dealer and blinds positions
        AnsiConsole.MarkupLine($"[yellow]Dealer: {game.GetDealer().Player.Name}[/]");
        AnsiConsole.MarkupLine($"[yellow]Small Blind: {game.GetSmallBlindPlayer().Player.Name} ({game.SmallBlind})[/]");
        AnsiConsole.MarkupLine($"[yellow]Big Blind: {game.GetBigBlindPlayer().Player.Name} ({game.BigBlind})[/]");

        // Post blinds
        AnsiConsole.MarkupLine("[yellow]Posting blinds...[/]");
        var blindActions = game.PostBlinds();
        foreach (var action in blindActions)
        {
            AnsiConsole.MarkupLine($"[dim]{action}[/]");
        }
        AnsiConsole.MarkupLine($"[green]Pot: {game.TotalPot}[/]");

        // Deal hole cards
        Logger.Paragraph("Preflop (4 hole cards)");
        game.DealHoleCards();
        DisplayAllHoleCards(game);

        // Preflop betting
        game.StartBettingRound();
        if (!RunBettingRound(game, "Preflop Betting"))
        {
            var result = game.PerformShowdown();
            DisplayShowdownResult(result, game);
            return;
        }

        // Flop
        if (game.CurrentPhase == OmahaPhase.Flop)
        {
            Logger.Paragraph("Flop");
            game.DealFlop();
            DisplayCommunityCards(game);
            
            game.StartBettingRound();
            if (!RunBettingRound(game, "Flop Betting"))
            {
                var result = game.PerformShowdown();
                DisplayShowdownResult(result, game);
                return;
            }
        }

        // Turn
        if (game.CurrentPhase == OmahaPhase.Turn)
        {
            Logger.Paragraph("Turn");
            game.DealTurn();
            DisplayCommunityCards(game);
            
            game.StartBettingRound();
            if (!RunBettingRound(game, "Turn Betting"))
            {
                var result = game.PerformShowdown();
                DisplayShowdownResult(result, game);
                return;
            }
        }

        // River
        if (game.CurrentPhase == OmahaPhase.River)
        {
            Logger.Paragraph("River");
            game.DealRiver();
            DisplayCommunityCards(game);
            
            game.StartBettingRound();
            if (!RunBettingRound(game, "River Betting"))
            {
                var result = game.PerformShowdown();
                DisplayShowdownResult(result, game);
                return;
            }
        }

        // Showdown
        if (game.CurrentPhase == OmahaPhase.Showdown)
        {
            var result = game.PerformShowdown();
            DisplayShowdownResult(result, game);
        }
    }

    private static bool RunBettingRound(OmahaGame game, string roundName)
    {
        var minBet = game.GetCurrentMinBet();

        while (!game.CurrentBettingRound.IsComplete)
        {
            var currentPlayer = game.GetCurrentPlayer();
            var available = game.GetAvailableActions();

            // Clear screen and show fresh game state for current player
            Logger.ClearScreen();
            Logger.Paragraph(roundName);
            AnsiConsole.MarkupLine($"[dim]Big Blind: {minBet}[/]");

            // Show community cards if any
            if (game.CommunityCards.Any())
            {
                AnsiConsole.MarkupLine("[dim]Community Cards:[/]");
                CardRenderer.RenderCards(game.CommunityCards);
                AnsiConsole.WriteLine();
            }

            AnsiConsole.MarkupLine($"[green]Pot: {game.TotalPot}[/] | [yellow]Current Bet: {game.CurrentBettingRound.CurrentBet}[/]");
            DisplayPlayerStatus(game, currentPlayer);

            // Show current player's hole cards
            var gamePlayer = game.GamePlayers.First(gp => gp.Player.Name == currentPlayer.Name);
            AnsiConsole.MarkupLine($"[cyan]{currentPlayer.Name}[/]'s hole cards:");
            CardRenderer.RenderCards(gamePlayer.HoleCards);
            AnsiConsole.MarkupLine($"[dim]({gamePlayer.HoleCards.ToStringRepresentation()})[/]");
            AnsiConsole.WriteLine();

            // Show live odds for the current player
            var deadCards = game.GamePlayers
                .Where(gp => gp.Player.HasFolded)
                .SelectMany(gp => gp.HoleCards)
                .ToList();
            LiveOddsRenderer.RenderOmahaOdds(
                gamePlayer.HoleCards.ToList(),
                game.CommunityCards.ToList(),
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

    private static void DisplayShowdownResult(OmahaShowdownResult result, OmahaGame game)
    {
        Logger.Paragraph("Showdown");

        if (result.WonByFold)
        {
            var winner = result.Payouts.First();
            AnsiConsole.MarkupLine($"[bold green]{winner.Key}[/] wins {winner.Value} chips (all others folded)!");
            return;
        }

        // Show community cards
        if (game.CommunityCards.Any())
        {
            AnsiConsole.MarkupLine("[dim]Community Cards:[/]");
            CardRenderer.RenderCards(game.CommunityCards);
            AnsiConsole.WriteLine();
        }

        // Show all hands
        if (result.PlayerHands != null)
        {
            foreach (var (playerName, (hand, holeCards, _)) in result.PlayerHands)
            {
                var handDescription = hand != null ? HandDescriptionFormatter.GetHandDescription(hand) : "Unknown";
                AnsiConsole.MarkupLine($"[cyan]{playerName}[/]:");
                AnsiConsole.MarkupLine("[dim]Hole Cards:[/]");
                CardRenderer.RenderCards(holeCards);
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

    private static void DisplayPlayerStacks(OmahaGame game)
    {
        var table = new Table();
        table.AddColumn("Player");
        table.AddColumn("Chips");
        table.AddColumn("Position");
        table.AddColumn("Status");

        for (int i = 0; i < game.GamePlayers.Count; i++)
        {
            var gamePlayer = game.GamePlayers[i];
            var player = gamePlayer.Player;
            var position = GetPositionName(game, i);
            var status = player.HasFolded ? "[red]Folded[/]" :
                        player.IsAllIn ? "[yellow]All-In[/]" :
                        "[green]Active[/]";
            table.AddRow(player.Name, player.ChipStack.ToString(), position, status);
        }

        AnsiConsole.Write(table);
    }

    private static string GetPositionName(OmahaGame game, int playerIndex)
    {
        if (playerIndex == game.DealerPosition)
            return "Dealer (BTN)";
        if (playerIndex == game.SmallBlindPosition)
            return "Small Blind (SB)";
        if (playerIndex == game.BigBlindPosition)
            return "Big Blind (BB)";
        return "";
    }

    private static void DisplayPlayerStatus(OmahaGame game, PokerPlayer currentPlayer)
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

    private static void DisplayAllHoleCards(OmahaGame game)
    {
        foreach (var gamePlayer in game.GamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                AnsiConsole.MarkupLine($"[cyan bold]{gamePlayer.Player.Name}[/]'s hole cards:");
                CardRenderer.RenderCards(gamePlayer.HoleCards);
                AnsiConsole.MarkupLine($"[dim]({gamePlayer.HoleCards.ToStringRepresentation()})[/]");
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void DisplayCommunityCards(OmahaGame game)
    {
        AnsiConsole.MarkupLine("[dim]Community Cards:[/]");
        CardRenderer.RenderCards(game.CommunityCards);
        AnsiConsole.MarkupLine($"[dim]({game.CommunityCards.ToStringRepresentation()})[/]");
    }

    private static void DisplayFinalStandings(OmahaGame game)
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
