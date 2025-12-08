using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.CLI.Extensions;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Games;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CardGames.Poker.CLI.Play.Api;

internal class ApiFiveCardDrawPlayCommand : AsyncCommand<ApiSettings>
{
	private readonly IFiveCardDrawApi _fiveCardDrawApi;
	private static readonly SpectreLogger Logger = new();

	public ApiFiveCardDrawPlayCommand(IFiveCardDrawApi fiveCardDrawApi)
	{
		_fiveCardDrawApi = fiveCardDrawApi;
	}
	protected override async Task<int> ExecuteAsync(CommandContext context, ApiSettings settings, CancellationToken cancellationToken)
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

		//Call the API to start a new Five Card Draw game
		var playerInfos = players
			.Select(p => new PlayerInfo(p.name, p.startingChips))
			.ToList();

		var command = new CreateGameCommand(ante, "Five Card Draw", minBet, playerInfos);
		var response = await _fiveCardDrawApi.CreateGameAsync(command, cancellationToken);

		if (!response.IsSuccessful)
		{
			AnsiConsole.MarkupLine("[red]Failed to create game via API.[/]");
			return 1;
		}

		var gameId = response.Content;

		Logger.Paragraph("Game Started");
		AnsiConsole.MarkupLine($"[dim]Ante: {ante} | Min Bet: {minBet}[/]");


		//do
		//{
		//	await PlayHand(gameId);
		//}
		//while (game.CanContinue() && AnsiConsole.Confirm("Play another hand?"));

		Logger.Paragraph("Game Over");
		//TODO:DisplayFinalStandings(game);

		return 0;

		//return result.IsSuccessful ? 0 : 1;
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

	private async Task PlayHand(Guid gameId)
	{
		Logger.Paragraph("New Hand");
		await DisplayPlayerStacksAsync(gameId);

		//Start hand
		var startHandResponse = await _fiveCardDrawApi.StartHandAsync(gameId);

		//Collect antes
		AnsiConsole.MarkupLine("[yellow]Collecting antes...[/]");

		var collectAntesResponse = await _fiveCardDrawApi.CollectAntesAsync(gameId);
		var collectAntesSuccessful = collectAntesResponse.Content;

		foreach (var anteContribution in collectAntesSuccessful.AnteContributions)
		{
			AnsiConsole.MarkupLine($"[dim]{anteContribution.PlayerName} : {anteContribution.Amount}[/]");
		}
		AnsiConsole.MarkupLine($"[green]Pot: {collectAntesSuccessful.TotalAntesCollected}[/]");

		//Deal hands
		AnsiConsole.MarkupLine("[yellow]Dealing cards...[/]");
		var dealHandsResponse = await _fiveCardDrawApi.DealHandsAsync(gameId);
		
		//Display hands(in a real game, only show current player's hand)
		await DisplayAllHandsAsync(gameId);

		//First betting round
		if (!RunBettingRound(gameId, "First Betting Round"))
		{
			//Hand ended due to folds
			//var result = game.PerformShowdown();
			//DisplayShowdownResult(result);
			return;
		}

		////Draw phase
		//if (game.CurrentPhase == FiveCardDrawPhase.DrawPhase)
		//{
		//	RunDrawPhase(game);
		//}

		////Second betting round
		//if (game.CurrentPhase == FiveCardDrawPhase.SecondBettingRound)
		//{
		//	if (!RunBettingRound(game, "Second Betting Round"))
		//	{
		//		var result = game.PerformShowdown();
		//		DisplayShowdownResult(result);
		//		return;
		//	}
		//}

		////Showdown
		//if (game.CurrentPhase == FiveCardDrawPhase.Showdown)
		//{
		//	var result = game.PerformShowdown();
		//	DisplayShowdownResult(result);
		//}
	}

	private async Task DisplayPlayerStacksAsync(Guid gameId)
	{
		var table = new Table();
		table.AddColumn("Player");
		table.AddColumn("Chips");
		table.AddColumn("Status");
		
		var playersResponse = await _fiveCardDrawApi.GetGamePlayersAsync(gameId);
		var players = playersResponse.Content;

		foreach (var gamePlayer in players)
		{
			var status = gamePlayer.HasFolded ? "[red]Folded[/]" :
				gamePlayer.IsAllIn ? "[yellow]All-In[/]" :
				"[green]Active[/]";

			table.AddRow(gamePlayer.PlayerName, gamePlayer.ChipStack.ToString(), status);
		}

		AnsiConsole.Write(table);
	}

	private async Task DisplayAllHandsAsync(Guid gameId)
	{
		var playersResponse = await _fiveCardDrawApi.GetGamePlayersAsync(gameId);
		var players = playersResponse.Content;

		foreach (var gamePlayer in players)
		{
			if (!gamePlayer.HasFolded)
			{
				AnsiConsole.MarkupLine($"[cyan bold]{gamePlayer.PlayerName}[/]'s hand:");
				ApiCardRenderer.RenderCards(gamePlayer.Hand);
				AnsiConsole.MarkupLine($"[dim]({gamePlayer.Hand.ToStringRepresentation()})[/]");
				AnsiConsole.WriteLine();
			}
		}
	}

	private static bool RunBettingRound(Guid gameId, string roundName)
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
}
