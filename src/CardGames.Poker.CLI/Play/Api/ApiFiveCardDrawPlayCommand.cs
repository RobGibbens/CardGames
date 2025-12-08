using System;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.CLI.Output;
using CardGames.Poker.Games;
using Spectre.Console;
using Spectre.Console.Cli;
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
	//private async Task PlayHand(Guid gameId)
	//{
	//	Logger.Paragraph("New Hand");
	//	DisplayPlayerStacks(gameId);

	//	//TODO: ROB - Call api
	//	//Start hand
	//	var startHandResponse = await _fiveCardDrawApi.StartHandAsync(gameId);

	//	//Collect antes
	//	AnsiConsole.MarkupLine("[yellow]Collecting antes...[/]");

	//	//TODO: ROB - Call api
	//	var collectAntesResponse = await _fiveCardDrawApi.CollectAntesAsync(gameId);
	//	var collectAntesSuccessful = collectAntesResponse.Content;
		
	//	foreach (var anteContribution in collectAntesSuccessful.AnteContributions)
	//	{
	//		AnsiConsole.MarkupLine($"[dim]{anteContribution.PlayerName} : {anteContribution.Amount}[/]");
	//	}
	//	AnsiConsole.MarkupLine($"[green]Pot: {collectAntesSuccessful.TotalAntesCollected}[/]");

	//	//Deal hands
	//	AnsiConsole.MarkupLine("[yellow]Dealing cards...[/]");
	//	game.DealHands();

	//	//Display hands(in a real game, only show current player's hand)
	//	DisplayAllHands(game);

	//	//First betting round
	//	if (!RunBettingRound(game, "First Betting Round"))
	//	{
	//		//Hand ended due to folds
	//		var result = game.PerformShowdown();
	//		DisplayShowdownResult(result);
	//		return;
	//	}

	//	//Draw phase
	//	if (game.CurrentPhase == FiveCardDrawPhase.DrawPhase)
	//	{
	//		RunDrawPhase(game);
	//	}

	//	//Second betting round
	//	if (game.CurrentPhase == FiveCardDrawPhase.SecondBettingRound)
	//	{
	//		if (!RunBettingRound(game, "Second Betting Round"))
	//		{
	//			var result = game.PerformShowdown();
	//			DisplayShowdownResult(result);
	//			return;
	//		}
	//	}

	//	//Showdown
	//	if (game.CurrentPhase == FiveCardDrawPhase.Showdown)
	//	{
	//		var result = game.PerformShowdown();
	//		DisplayShowdownResult(result);
	//	}
	//}

	//private static void DisplayPlayerStacks(Guid gameId)
	//{
	//	var table = new Table();
	//	table.AddColumn("Player");
	//	table.AddColumn("Chips");
	//	table.AddColumn("Status");

	//	foreach (var gamePlayer in game.GamePlayers)
	//	{
	//		var player = gamePlayer.Player;
	//		var status = player.HasFolded ? "[red]Folded[/]" :
	//			player.IsAllIn ? "[yellow]All-In[/]" :
	//			"[green]Active[/]";
	//		table.AddRow(player.Name, player.ChipStack.ToString(), status);
	//	}

	//	AnsiConsole.Write(table);
	//}
}
