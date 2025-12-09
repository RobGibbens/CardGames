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
using CardGames.Core.French.Cards;
using CardGames.Poker.CLI.Api;
using BettingActionType = CardGames.Poker.Api.Contracts.BettingActionType;

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

	private async Task PlayHandAsync(Guid gameId)
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
		if (!(await RunBettingRoundAsync(gameId, "First Betting Round")))
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
		//	if (!RunBettingRoundAsync(game, "Second Betting Round"))
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

	private async Task<bool> RunBettingRoundAsync(Guid gameId, string roundName)
	{
		bool currentBettingRoundIsComplete = false;
		var currentBettingRoundResponse = await _fiveCardDrawApi.GetCurrentBettingRoundAsync(gameId);
		var currentBettingRound = currentBettingRoundResponse.Content;
		currentBettingRoundIsComplete = currentBettingRound.IsComplete;

		while (!currentBettingRoundIsComplete)
		{
			var gamePlayersResponse = await _fiveCardDrawApi.GetGamePlayersAsync(gameId);
			var gamePlayers = gamePlayersResponse.Content;
			var currentPlayerTurnResponse = await _fiveCardDrawApi.GetCurrentPlayerTurnAsync(gameId);
			var currentPlayerTurn = currentPlayerTurnResponse.Content;
			var currentPlayer = currentPlayerTurn.Player;
			var available = currentPlayerTurn.AvailableActions;

			//// Clear screen and show fresh game state for current player
			Logger.ClearScreen();
			Logger.Paragraph(roundName);

			AnsiConsole.MarkupLine($"[green]Pot: {currentBettingRound.TotalPot}[/] | [yellow]Current Bet: {currentBettingRound.CurrentBet}[/]");
			DisplayPlayerStatus(gamePlayers, currentPlayer.PlayerName);

			// Show current player's hand
			var gamePlayer = gamePlayers.First(gp => gp.PlayerName == currentPlayer.PlayerName);
			AnsiConsole.MarkupLine($"[cyan]{currentPlayer.PlayerName}[/]'s hand:");
			ApiCardRenderer.RenderCards(gamePlayer.Hand);
			AnsiConsole.MarkupLine($"[dim]({gamePlayer.Hand.ToStringRepresentation()})[/]");
			AnsiConsole.WriteLine();

			// Show live odds for the current player
			var deadCards = gamePlayers
				.Where(gp => gp.HasFolded)
				.SelectMany(gp => gp.Hand)
				.ToList();
			
			LiveOddsRenderer.RenderDrawOdds(
				gamePlayer.Hand.Select(dc => new Card((Suit)dc.Suit!.Value, (Symbol)dc.Symbol!.Value)).ToList(),
				deadCards.Select(dc => new Card((Suit)dc.Suit!.Value, (Symbol)dc.Symbol!.Value)).ToList());

			AnsiConsole.WriteLine();

			var action = PromptForAction(currentPlayer, available);
			
			var request = new ProcessBettingActionRequest(action.ActionType, action.Amount);
			var processActionResponse = await _fiveCardDrawApi.ProcessBettingActionAsync(gameId, request);
			var processActionResult = processActionResponse.Content;

			// if (!result.Success)
			// {
			// 	AnsiConsole.MarkupLine($"[red]{result.ErrorMessage}[/]");
			// 	continue;
			// }

			AnsiConsole.MarkupLine($"[blue]{processActionResult.Action}[/]");

			// Check if only one player remains
			if (currentBettingRound.PlayersInHand <= 1)
			{
				return false;
			}

			currentBettingRoundResponse = await _fiveCardDrawApi.GetCurrentBettingRoundAsync(gameId);
			currentBettingRound = currentBettingRoundResponse.Content;
			currentBettingRoundIsComplete = currentBettingRound.IsComplete;
		}

		return true;
	}
	
	private void DisplayPlayerStatus(ICollection<GetGamePlayersResponse> gamePlayers, string currentPlayerName)
	{
		var playersInfo = gamePlayers.Select(gp =>
		{
			var marker = gp.PlayerName == currentPlayerName ? "►" : " ";
			var status = gp.HasFolded ? "(folded)" :
				gp.IsAllIn ? "(all-in)" : "";
			var bet = gp.CurrentBet > 0 ? $"bet: {gp.CurrentBet}" : "";
			return $"{marker} {gp.PlayerName}: {gp.ChipStack} chips {bet} {status}";
		});

		AnsiConsole.MarkupLine($"[dim]{string.Join(" | ", playersInfo)}[/]");
	}
	
	private static (BettingActionType ActionType, int Amount) PromptForAction(CurrentPlayerResponse player, AvailableActionsResponse available)
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
				.Title($"[cyan]{player.PlayerName}[/] ({player.ChipStack} chips) - Your action:")
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
}
