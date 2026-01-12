using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Betting;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Games.KingsAndLows;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CardGames.Poker.CLI.Play;

internal class KingsAndLowsPlayCommand : Command<KingsAndLowsPlaySettings>
{
    private static readonly SpectreLogger Logger = new();

    protected override int Execute(CommandContext context, KingsAndLowsPlaySettings settings, CancellationToken cancellationToken)
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

        var startingChips = settings.StartingChips == default
            ? AnsiConsole.Ask<int>("Starting chips per player: ", 1000)
            : settings.StartingChips;

        var ante = settings.Ante == default
            ? AnsiConsole.Ask<int>("Ante amount: ", 10)
            : settings.Ante;

        var kingRequired = settings.KingRequired || AnsiConsole.Confirm("Require King to use low card as wild?", false);

        var anteEveryHand = settings.AnteEveryHand || AnsiConsole.Confirm("Ante every hand (vs single ante at start)?", false);

        var playerNames = GetPlayerNames(numberOfPlayers);
        var players = playerNames.Select(name => (name, startingChips)).ToList();

        var game = new KingsAndLowsGame(players, ante, kingRequired, anteEveryHand);

        Logger.Paragraph("Kings and Lows - Game Started");
        AnsiConsole.MarkupLine($"[dim]Ante: {ante}[/]");
        if (kingRequired)
        {
            AnsiConsole.MarkupLine("[dim]Rule: King required to use low card as wild[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Rule: Lowest card in hand is always wild[/]");
        }
        AnsiConsole.MarkupLine("[dim]Kings are always wild[/]");

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

    private static void PlayHand(KingsAndLowsGame game)
    {
        Logger.Paragraph("New Hand");
        DisplayPlayerStacks(game);

        // Start hand
        game.StartHand();

        // Collect antes (if in that phase)
        if (game.CurrentPhase == Phases.CollectingAntes)
        {
            AnsiConsole.MarkupLine("[yellow]Collecting antes...[/]");
            var anteActions = game.CollectAntes();
            foreach (var action in anteActions)
            {
                AnsiConsole.MarkupLine($"[dim]{action}[/]");
            }
        }
        AnsiConsole.MarkupLine($"[green]Pot: {game.CurrentPot}[/]");

        // Deal hands
        AnsiConsole.MarkupLine("[yellow]Dealing cards...[/]");
        game.DealHands();

        // Display all hands
        DisplayAllHands(game);

        // Drop or Stay phase - the RunDropOrStayPhase method handles screen clearing per player
        RunDropOrStayPhase(game);

        var dropResult = game.FinalizeDropOrStay();

        if (dropResult.AllDropped)
        {
            AnsiConsole.MarkupLine("[yellow]All players dropped! Dead hand - pot stays for next hand.[/]");
            return;
        }

        // Display who stayed and who dropped
        Logger.ClearScreen();
        Logger.Paragraph("Drop or Stay Results");
        if (dropResult.DroppedPlayerNames.Any())
        {
            AnsiConsole.MarkupLine($"[red]Dropped:[/] {string.Join(", ", dropResult.DroppedPlayerNames)}");
        }
        if (dropResult.StayingPlayerNames.Any())
        {
            AnsiConsole.MarkupLine($"[green]Stayed:[/] {string.Join(", ", dropResult.StayingPlayerNames)}");
        }

        // Draw phase
        if (game.CurrentPhase == Phases.DrawPhase)
        {
            RunDrawPhase(game);
        }

        // Player vs Deck
        if (game.CurrentPhase == Phases.PlayerVsDeck)
        {
            RunPlayerVsDeck(game);
        }

        // Showdown
        if (game.CurrentPhase == Phases.Showdown)
        {
            var showdownResult = game.PerformShowdown();
            DisplayShowdownResult(showdownResult, game);

            // Pot matching
            if (game.CurrentPhase == Phases.PotMatching)
            {
                RunPotMatching(game, showdownResult);
            }
        }
    }

    private static void RunDropOrStayPhase(KingsAndLowsGame game)
    {
        foreach (var gamePlayer in game.GamePlayers)
        {
            // Clear screen and show fresh game state for current player's decision
            Logger.ClearScreen();
            Logger.Paragraph("Drop or Stay Decision");
            AnsiConsole.MarkupLine("[cyan]On the count of 3, decide to DROP or STAY.[/]");
            AnsiConsole.MarkupLine("[dim]Players who DROP pay nothing more for this hand.[/]");
            AnsiConsole.MarkupLine("[dim]Players who STAY continue to draw and showdown.[/]");
            AnsiConsole.MarkupLine($"[green]Pot: {game.CurrentPot}[/]");
            AnsiConsole.WriteLine();

            // Show the player's hand
            AnsiConsole.MarkupLine($"[cyan bold]{gamePlayer.Player.Name}[/]'s hand:");
            var wildCards = game.GetPlayerWildCards(gamePlayer.Player.Name);
            CardRenderer.RenderCards(gamePlayer.Hand, wildCards: wildCards);
            AnsiConsole.MarkupLine($"[dim]({gamePlayer.Hand.ToStringRepresentation()})[/]");
            if (wildCards.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]Wild cards: {wildCards.ToStringRepresentation()}[/]");
            }
            AnsiConsole.WriteLine();

            // Show live odds for the player (Kings and Lows: Kings + lowest card are wild)
            LiveOddsRenderer.RenderKingsAndLowsOdds(
                gamePlayer.Hand.ToList(),
                game.WildCardRules.KingRequired);
            AnsiConsole.WriteLine();

            var decision = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[cyan]{gamePlayer.Player.Name}[/] ({gamePlayer.Player.ChipStack} chips) - Your decision:")
                    .AddChoices("Stay", "Drop"));

            var dropOrStay = decision == "Stay" ? DropOrStayDecision.Stay : DropOrStayDecision.Drop;
            game.SetPlayerDecision(gamePlayer.Player.Name, dropOrStay);

            AnsiConsole.MarkupLine($"[blue]{gamePlayer.Player.Name} decides to {decision.ToUpper()}[/]");
            AnsiConsole.WriteLine();
        }
    }

    private static void RunDrawPhase(KingsAndLowsGame game)
    {
        while (game.CurrentPhase == Phases.DrawPhase)
        {
            var drawPlayer = game.GetCurrentDrawPlayer();
            if (drawPlayer == null)
            {
                break;
            }

            // Clear screen and show fresh game state for draw phase
            Logger.ClearScreen();
            Logger.Paragraph("Draw Phase");
            AnsiConsole.MarkupLine("[dim]Players may discard up to 5 cards and draw replacements.[/]");
            AnsiConsole.MarkupLine($"[green]Pot: {game.CurrentPot}[/]");
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[cyan]{drawPlayer.Player.Name}[/]'s turn to draw:");
            var wildCards = game.GetPlayerWildCards(drawPlayer.Player.Name);
            CardRenderer.RenderCards(drawPlayer.Hand, wildCards: wildCards);

            // Display cards with indices
            AnsiConsole.MarkupLine("[dim]Card positions: 0, 1, 2, 3, 4[/]");
            AnsiConsole.MarkupLine($"[dim]({drawPlayer.Hand.ToStringRepresentation()})[/]");
            if (wildCards.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]Wild cards: {wildCards.ToStringRepresentation()}[/]");
            }

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
                var newWildCards = game.GetPlayerWildCards(drawPlayer.Player.Name);
                CardRenderer.RenderCards(drawPlayer.Hand, wildCards: newWildCards);
                AnsiConsole.MarkupLine($"[dim]({drawPlayer.Hand.ToStringRepresentation()})[/]");
                if (newWildCards.Any())
                {
                    AnsiConsole.MarkupLine($"[yellow]Wild cards: {newWildCards.ToStringRepresentation()}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[blue]{drawPlayer.Player.Name} stands pat[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    private static void RunPlayerVsDeck(KingsAndLowsGame game)
    {
        Logger.Paragraph("Player vs Deck");
        AnsiConsole.MarkupLine("[yellow]Only one player stayed - they must beat the deck![/]");

        var solePlayer = game.GamePlayers.FirstOrDefault(gp => gp.HasStayed);
        if (solePlayer == null)
        {
            return;
        }

        // Show player's hand
        AnsiConsole.MarkupLine($"[cyan]{solePlayer.Player.Name}[/]'s hand:");
        var playerWildCards = game.GetPlayerWildCards(solePlayer.Player.Name);
        CardRenderer.RenderCards(solePlayer.Hand, wildCards: playerWildCards);
        AnsiConsole.MarkupLine($"[dim]({solePlayer.Hand.ToStringRepresentation()})[/]");
        if (playerWildCards.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]Wild cards: {playerWildCards.ToStringRepresentation()}[/]");
        }
        AnsiConsole.WriteLine();

        // Show deck's initial hand
        AnsiConsole.MarkupLine("[magenta]Deck's hand:[/]");
        var deckWildCards = game.GetDeckHandWildCards();
        CardRenderer.RenderCards(game.DeckHand, wildCards: deckWildCards);
        AnsiConsole.MarkupLine("[dim]Card positions: 0, 1, 2, 3, 4[/]");
        AnsiConsole.MarkupLine($"[dim]({game.DeckHand.ToStringRepresentation()})[/]");
        if (deckWildCards.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]Wild cards: {deckWildCards.ToStringRepresentation()}[/]");
        }
        AnsiConsole.WriteLine();

        // Dealer chooses which cards to discard for the deck
        AnsiConsole.MarkupLine("[yellow]Dealer: Choose which cards to discard for the deck's draw.[/]");
        var discardInput = AnsiConsole.Ask<string>("Enter positions to discard for deck (0-4, space-separated) or press Enter for deck to stand pat: ", "");

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

        var deckDrawResult = game.ProcessDeckDrawManual(discardIndices);
        
        if (!deckDrawResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]{deckDrawResult.ErrorMessage}[/]");
            return;
        }

        if (discardIndices.Count > 0)
        {
            AnsiConsole.MarkupLine($"[blue]Deck discards {deckDrawResult.DiscardedCards.Count} card(s) and draws {deckDrawResult.NewCards.Count} card(s)[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[blue]Deck stands pat[/]");
        }
        
        // Show deck's final hand
        AnsiConsole.MarkupLine("[magenta]Deck's final hand:[/]");
        var finalDeckWildCards = game.GetDeckHandWildCards();
        CardRenderer.RenderCards(game.DeckHand, wildCards: finalDeckWildCards);
        AnsiConsole.MarkupLine($"[dim]({game.DeckHand.ToStringRepresentation()})[/]");
        if (finalDeckWildCards.Any())
        {
            AnsiConsole.MarkupLine($"[yellow]Wild cards: {finalDeckWildCards.ToStringRepresentation()}[/]");
        }
        AnsiConsole.WriteLine();
    }

    private static void DisplayShowdownResult(KingsAndLowsShowdownResult result, KingsAndLowsGame game)
    {
        Logger.Paragraph("Showdown");

        // Show all hands with evaluations
        foreach (var (playerName, (hand, cards)) in result.PlayerHands)
        {
            var handDescription = hand != null ? HandDescriptionFormatter.GetHandDescription(hand) : "Unknown";
            
            if (playerName == "Deck")
            {
                AnsiConsole.MarkupLine("[magenta]Deck:[/]");
                var deckWildCards = game.GetDeckHandWildCards();
                CardRenderer.RenderCards(cards, wildCards: deckWildCards);
            }
            else
            {
                AnsiConsole.MarkupLine($"[cyan]{playerName}[/]:");
                var wildCards = game.GetPlayerWildCards(playerName);
                CardRenderer.RenderCards(cards, wildCards: wildCards);
            }
            AnsiConsole.MarkupLine($"[magenta]{handDescription}[/]");
            AnsiConsole.WriteLine();
        }

        // Show winners
        Logger.Paragraph("Result");
        
        if (result.DeckWon)
        {
            AnsiConsole.MarkupLine("[red]Deck wins![/]");
            var loser = result.Losers.FirstOrDefault();
            if (loser != null)
            {
                AnsiConsole.MarkupLine($"[yellow]{loser} must match the pot ({result.PotBeforeShowdown} chips)[/]");
            }
        }
        else if (result.IsTie)
        {
            var winnerNames = string.Join(" and ", result.Winners);
            AnsiConsole.MarkupLine($"[bold green]Tie! {winnerNames} split the pot![/]");
            foreach (var (playerName, amount) in result.Payouts)
            {
                AnsiConsole.MarkupLine($"[green]{playerName} wins {amount} chips[/]");
            }
        }
        else
        {
            var winner = result.Winners.FirstOrDefault();
            if (winner != null && result.Payouts.TryGetValue(winner, out var amount))
            {
                AnsiConsole.MarkupLine($"[bold green]{winner} wins {amount} chips![/]");
            }
        }

        // Show losers who need to match
        if (result.Losers.Any() && !result.DeckWon)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Losers must match the pot:[/]");
            foreach (var loser in result.Losers)
            {
                if (result.PotMatchAmounts.TryGetValue(loser, out var matchAmount))
                {
                    AnsiConsole.MarkupLine($"[yellow]  {loser}: {matchAmount} chips[/]");
                }
            }
        }
    }

    private static void RunPotMatching(KingsAndLowsGame game, KingsAndLowsShowdownResult showdownResult)
    {
        Logger.Paragraph("Pot Matching");
        
        AnsiConsole.MarkupLine("[yellow]Losers are matching the pot...[/]");
        var matchResult = game.ProcessPotMatching();

        if (matchResult.Success)
        {
            foreach (var action in matchResult.MatchActions)
            {
                AnsiConsole.MarkupLine($"[dim]{action.PlayerName} matches {action.Amount} chips[/]");
            }
            AnsiConsole.MarkupLine($"[green]New pot for next hand: {matchResult.NewPotAmount} chips[/]");
        }
    }

    private static void DisplayPlayerStacks(KingsAndLowsGame game)
    {
        var table = new Table();
        table.AddColumn("Player");
        table.AddColumn("Chips");
        table.AddColumn("Status");

        foreach (var gamePlayer in game.GamePlayers)
        {
            var player = gamePlayer.Player;
            var status = player.ChipStack == 0 ? "[red]Busted[/]" : "[green]Active[/]";
            table.AddRow(player.Name, player.ChipStack.ToString(), status);
        }

        AnsiConsole.Write(table);
    }

    private static void DisplayAllHands(KingsAndLowsGame game)
    {
        foreach (var gamePlayer in game.GamePlayers)
        {
            AnsiConsole.MarkupLine($"[cyan bold]{gamePlayer.Player.Name}[/]'s hand:");
            var wildCards = game.GetPlayerWildCards(gamePlayer.Player.Name);
            CardRenderer.RenderCards(gamePlayer.Hand, wildCards: wildCards);
            AnsiConsole.MarkupLine($"[dim]({gamePlayer.Hand.ToStringRepresentation()})[/]");
            if (wildCards.Any())
            {
                AnsiConsole.MarkupLine($"[yellow]Wild cards: {wildCards.ToStringRepresentation()}[/]");
            }
            AnsiConsole.WriteLine();
        }
    }

    private static void DisplayFinalStandings(KingsAndLowsGame game)
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
