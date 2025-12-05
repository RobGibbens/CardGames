using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Poker.CLI.Play;

using CardGames.Poker.CLI.Api;
using CardGames.Poker.CLI.Output;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// CLI command that plays 5-Card Draw using the Web API backend.
/// </summary>
internal class ApiFiveCardDrawPlayCommand : AsyncCommand<ApiPlaySettings>
{
	private static readonly SpectreLogger Logger = new();
	private const int DrawPhaseDelayMs = 500;

	protected override async Task<int> ExecuteAsync(CommandContext context, ApiPlaySettings settings, CancellationToken cancellationToken)
	{
		Logger.LogApplicationStart();

		var apiUrl = settings.ApiUrl ?? "https://localhost:7034";
		using var apiClient = new ApiClient(apiUrl);

		try
		{
			// Phase 1: Create game and add players
			var (gameId, playerIds) = await SetupGameAsync(apiClient, settings);
			if (gameId == null)
			{
				return 1;
			}

			// Phase 2 & 3: Play hands with full game loop
			var continueGame = true;
			while (continueGame)
			{
				var handResult = await PlayHandAsync(apiClient, gameId.Value, playerIds, cancellationToken);
				if (handResult != 0)
				{
					return handResult;
				}

				// Check if game can continue
				var continueStatus = await apiClient.ContinueGameAsync(gameId.Value);
				if (continueStatus == null || !continueStatus.CanContinue)
				{
					if (continueStatus != null && continueStatus.WinnerName != null)
					{
						AnsiConsole.MarkupLine($"\n[green bold]Game Over! {continueStatus.WinnerName} wins with {continueStatus.WinnerChips} chips![/]");
					}
					else
					{
						AnsiConsole.MarkupLine("\n[yellow]Game cannot continue - not enough players with chips.[/]");
					}
					continueGame = false;
				}
				else
				{
					continueGame = AnsiConsole.Confirm("Play another hand?");
				}
			}

			Logger.Paragraph("Game Over");
			await DisplayFinalStandingsAsync(apiClient, gameId.Value);

			return 0;
		}
		catch (HttpRequestException ex)
		{
			AnsiConsole.MarkupLine($"[red]API Error: {ex.Message}[/]");
			AnsiConsole.MarkupLine("[dim]Make sure the API server is running.[/]");
			return 1;
		}
	}

	private static async Task<(Guid? gameId, List<(Guid id, string name)> playerIds)> SetupGameAsync(
		ApiClient apiClient,
		ApiPlaySettings settings)
	{
		var playerIds = new List<(Guid id, string name)>();

		// Step 1: Create the game
		var createRequest = new CreateGameRequest(
			GameType: GameType.FiveCardDraw,
			Configuration: new CreateGameConfigurationRequest(
				Ante: settings.Ante == default ? 10 : settings.Ante,
				MinBet: settings.MinBet == default ? 20 : settings.MinBet,
				StartingChips: settings.StartingChips == default ? 1000 : settings.StartingChips,
				MaxPlayers: 6
			)
		);

		var createResponse = await apiClient.CreateGameAsync(createRequest);
		if (createResponse == null)
		{
			AnsiConsole.MarkupLine("[red]Failed to create game.[/]");
			return (null, playerIds);
		}

		AnsiConsole.MarkupLine($"[green]Game created![/] ID: {createResponse.GameId}");
		AnsiConsole.MarkupLine($"[dim]Ante: {createResponse.Configuration.Ante} | Min Bet: {createResponse.Configuration.MinBet}[/]");

		// Step 2: Add players
		var numberOfPlayers = settings.NumberOfPlayers == default
			? AnsiConsole.Ask<int>("How many players? (2-6): ")
			: settings.NumberOfPlayers;

		if (numberOfPlayers < 2 || numberOfPlayers > 6)
		{
			AnsiConsole.MarkupLine("[red]Invalid number of players. Must be between 2 and 6.[/]");
			return (null, playerIds);
		}

		for (int i = 1; i <= numberOfPlayers; i++)
		{
			var playerName = AnsiConsole.Ask<string>($"Player {i} name: ");

			var joinRequest = new JoinGameRequest(
				PlayerName: playerName,
				BuyIn: createResponse.Configuration.StartingChips
			);

			var joinResponse = await apiClient.JoinGameAsync(createResponse.GameId, joinRequest);
			if (joinResponse == null)
			{
				AnsiConsole.MarkupLine($"[red]Failed to add player {playerName}.[/]");
				return (null, playerIds);
			}

			playerIds.Add((joinResponse.PlayerId, joinResponse.Name));
			AnsiConsole.MarkupLine($"[green]{joinResponse.Name}[/] joined (Position: {joinResponse.Position}, Chips: {joinResponse.ChipStack})");
		}

		// Step 3: Display game state
		Logger.Paragraph("Game Ready");

		var gameState = await apiClient.GetGameStateAsync(createResponse.GameId);
		if (gameState == null)
		{
			AnsiConsole.MarkupLine("[red]Failed to retrieve game state.[/]");
			return (null, playerIds);
		}

		DisplayGameState(gameState);

		return (createResponse.GameId, playerIds);
	}

	private static async Task<int> PlayHandAsync(
		ApiClient apiClient,
		Guid gameId,
		List<(Guid id, string name)> playerIds,
		CancellationToken cancellationToken)
	{
		Logger.Paragraph("New Hand");

		// Start hand (triggers ante collection and dealing automatically)
		var startResponse = await apiClient.StartHandAsync(gameId);
		if (startResponse == null)
		{
			AnsiConsole.MarkupLine("[red]Failed to start hand.[/]");
			return 1;
		}

		AnsiConsole.MarkupLine($"[green]Hand #{startResponse.HandNumber} started[/]");
		AnsiConsole.MarkupLine($"[dim]Dealer: Position {startResponse.DealerPosition}[/]");

		// Allow time for dealing to complete
		await Task.Delay(500, cancellationToken);

		// Get current hand state
		var handState = await apiClient.GetCurrentHandAsync(gameId);
		if (handState == null)
		{
			AnsiConsole.MarkupLine("[red]Failed to get hand state.[/]");
			return 1;
		}

		AnsiConsole.MarkupLine($"[green]Pot: {handState.Pot}[/]");

		// First betting round
		var firstRoundResult = await RunBettingRoundAsync(apiClient, gameId, playerIds, "First Betting Round");
		if (!firstRoundResult.continueHand)
		{
			await ShowHandResultsAsync(apiClient, gameId, playerIds);
			return 0;
		}

		// Check hand state to determine phase
		handState = await apiClient.GetCurrentHandAsync(gameId);
		if (handState?.Phase == "DrawPhase")
		{
			// Draw phase
			var drawResult = await RunDrawPhaseAsync(apiClient, gameId, playerIds);
			if (!drawResult)
			{
				await ShowHandResultsAsync(apiClient, gameId, playerIds);
				return 0;
			}
		}

		// Second betting round
		var secondRoundResult = await RunBettingRoundAsync(apiClient, gameId, playerIds, "Second Betting Round");
		if (!secondRoundResult.continueHand)
		{
			await ShowHandResultsAsync(apiClient, gameId, playerIds);
			return 0;
		}

		// Showdown
		await ShowHandResultsAsync(apiClient, gameId, playerIds);

		return 0;
	}

	private static async Task<bool> RunDrawPhaseAsync(
		ApiClient apiClient,
		Guid gameId,
		List<(Guid id, string name)> playerIds)
	{
		Logger.Paragraph("Draw Phase");

		while (true)
		{
			var handState = await apiClient.GetCurrentHandAsync(gameId);
			if (handState == null)
			{
				AnsiConsole.MarkupLine("[red]Failed to get hand state.[/]");
				return false;
			}

			// Check if draw phase is complete
			if (handState.Phase != "DrawPhase")
			{
				return true;
			}

			if (handState.CurrentPlayerToAct == null)
			{
				return true;
			}

			var currentPlayerId = handState.CurrentPlayerToAct.Value;
			var currentPlayer = playerIds.FirstOrDefault(p => p.id == currentPlayerId);

			// Display game state
			Logger.ClearScreen();
			Logger.Paragraph("Draw Phase");
			AnsiConsole.MarkupLine($"[green]Pot: {handState.Pot}[/]");
			DisplayHandPlayers(handState.Players, currentPlayerId);

			// Show current player's cards
			var cards = await apiClient.GetPlayerCardsAsync(gameId, currentPlayerId);
			if (cards != null && cards.Cards.Count > 0)
			{
				AnsiConsole.MarkupLine($"\n[cyan]{currentPlayer.name}[/]'s hand:");
				DisplayPlayerCards(cards.Cards);
			}

			// Prompt for cards to discard
			var discardIndices = PromptForDiscard(currentPlayer.name, cards?.Cards ?? []);

			// Submit draw action
			var request = new DrawCardsApiRequest(currentPlayerId, discardIndices);
			var result = await apiClient.DrawCardsAsync(gameId, request);

			if (result == null || !result.Success)
			{
				AnsiConsole.MarkupLine($"[red]{result?.ErrorMessage ?? "Failed to process draw"}[/]");
				continue;
			}

			AnsiConsole.MarkupLine($"[blue]{currentPlayer.name} discards {result.CardsDiscarded} card(s)[/]");
			if (result.NewCards.Count > 0)
			{
				AnsiConsole.MarkupLine($"[green]Drew: {string.Join(" ", result.NewCards)}[/]");
			}

			// Show new hand
			AnsiConsole.MarkupLine($"[cyan]New hand: {string.Join(" ", result.NewHand)}[/]");

			if (result.DrawPhaseComplete)
			{
				AnsiConsole.MarkupLine("[green]Draw phase complete![/]");
				return true;
			}

			// Brief pause before next player
			await Task.Delay(DrawPhaseDelayMs);
		}
	}

	private static void DisplayPlayerCards(List<string> cards)
	{
		var table = new Table();
		for (int i = 0; i < cards.Count; i++)
		{
			table.AddColumn($"[{i + 1}]");
		}

		table.AddRow(cards.Select(c => $"[bold]{c}[/]").ToArray());
		AnsiConsole.Write(table);
	}

	private static List<int> PromptForDiscard(string playerName, List<string> cards)
	{
		AnsiConsole.MarkupLine($"\n[cyan]{playerName}[/], select cards to discard (0-3 cards):");
		AnsiConsole.MarkupLine("[dim]Enter card positions (1-5) separated by spaces, or press Enter to keep all.[/]");

		var input = AnsiConsole.Prompt(
			new TextPrompt<string>("Cards to discard (e.g., '1 3' or leave blank):")
				.AllowEmpty());

		if (string.IsNullOrWhiteSpace(input))
		{
			return [];
		}

		var indices = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Select(s =>
			{
				if (int.TryParse(s, out var num) && num >= 1 && num <= cards.Count)
				{
					return num - 1; // Convert to 0-indexed
				}
				return -1;
			})
			.Where(i => i >= 0 && i < cards.Count)
			.Distinct()
			.Take(3) // Max 3 cards
			.ToList();

		return indices;
	}

	private static async Task<(bool continueHand, bool success)> RunBettingRoundAsync(
		ApiClient apiClient,
		Guid gameId,
		List<(Guid id, string name)> playerIds,
		string roundName)
	{
		Logger.Paragraph(roundName);

		while (true)
		{
			var handState = await apiClient.GetCurrentHandAsync(gameId);
			if (handState == null)
			{
				AnsiConsole.MarkupLine("[red]Failed to get hand state.[/]");
				return (false, false);
			}

			if (handState.CurrentPlayerToAct == null)
			{
				// Round complete
				return (true, true);
			}

			var currentPlayerId = handState.CurrentPlayerToAct.Value;
			var currentPlayer = playerIds.FirstOrDefault(p => p.id == currentPlayerId);

			// Display game state
			Logger.ClearScreen();
			Logger.Paragraph(roundName);
			AnsiConsole.MarkupLine($"[green]Pot: {handState.Pot}[/] | [yellow]Current Bet: {handState.CurrentBet}[/]");
			DisplayHandPlayers(handState.Players, currentPlayerId);

			// Show current player's cards
			var cards = await apiClient.GetPlayerCardsAsync(gameId, currentPlayerId);
			if (cards != null && cards.Cards.Count > 0)
			{
				AnsiConsole.MarkupLine($"\n[cyan]{currentPlayer.name}[/]'s hand: [bold]{string.Join(" ", cards.Cards)}[/]");
			}

			// Get available actions
			var actionsResponse = await apiClient.GetAvailableActionsAsync(gameId, currentPlayerId);
			if (actionsResponse == null || !actionsResponse.IsCurrentPlayer)
			{
				continue;
			}

			// Prompt for action
			var action = PromptForAction(currentPlayer.name, actionsResponse.Actions);

			// Submit action
			var request = new PlaceActionRequest(currentPlayerId, action.actionType, action.amount);
			var result = await apiClient.PlaceActionAsync(gameId, request);

			if (result == null || !result.Success)
			{
				AnsiConsole.MarkupLine($"[red]{result?.ErrorMessage ?? "Failed to process action"}[/]");
				continue;
			}

			AnsiConsole.MarkupLine($"[blue]{result.ActionDescription}[/]");

			if (result.RoundComplete)
			{
				return (result.CurrentPhase != "Showdown" && result.CurrentPhase != "Complete", true);
			}

			// Check if only one player remains
			var activePlayers = handState.Players.Count(p => p.Status != "Folded");
			if (activePlayers <= 1)
			{
				return (false, true);
			}
		}
	}

	private static (BettingActionType actionType, int amount) PromptForAction(
		string playerName,
		AvailableActionsDto available)
	{
		var choices = new List<string>();

		if (available.CanCheck)
		{
			choices.Add("Check");
		}

		if (available.CanBet)
		{
			choices.Add($"Bet ({available.MinBet}-{available.MaxBet})");
		}

		if (available.CanCall)
		{
			choices.Add($"Call {available.CallAmount}");
		}

		if (available.CanRaise)
		{
			choices.Add($"Raise (min {available.MinRaise})");
		}

		if (available.CanFold)
		{
			choices.Add("Fold");
		}

		if (available.CanAllIn)
		{
			choices.Add($"All-In ({available.MaxBet})");
		}

		if (choices.Count == 0)
		{
			choices.Add("Check"); // Fallback
		}

		var choice = AnsiConsole.Prompt(
			new SelectionPrompt<string>()
				.Title($"[cyan]{playerName}[/] - Your action:")
				.AddChoices(choices));

		if (choice == "Check")
		{
			return (BettingActionType.Check, 0);
		}

		if (choice.StartsWith("Call"))
		{
			return (BettingActionType.Call, available.CallAmount);
		}

		if (choice == "Fold")
		{
			return (BettingActionType.Fold, 0);
		}

		if (choice.StartsWith("All-In"))
		{
			return (BettingActionType.AllIn, available.MaxBet);
		}

		if (choice.StartsWith("Bet"))
		{
			var amount = AnsiConsole.Ask<int>($"Bet amount ({available.MinBet}-{available.MaxBet}): ");
			return (BettingActionType.Bet, Math.Clamp(amount, available.MinBet, available.MaxBet));
		}

		if (choice.StartsWith("Raise"))
		{
			var amount = AnsiConsole.Ask<int>($"Raise to (min {available.MinRaise}): ");
			return (BettingActionType.Raise, Math.Max(amount, available.MinRaise));
		}

		return (BettingActionType.Fold, 0);
	}

	private static void DisplayHandPlayers(List<HandPlayerStateResponse> players, Guid currentPlayerId)
	{
		var table = new Table();
		table.AddColumn("");
		table.AddColumn("Player");
		table.AddColumn("Chips");
		table.AddColumn("Bet");
		table.AddColumn("Status");
		table.AddColumn("Cards");

		foreach (var player in players)
		{
			var marker = player.PlayerId == currentPlayerId ? "[green]?[/]" : " ";
			var status = player.Status switch
			{
				"Folded" => "[dim](folded)[/]",
				"AllIn" => "[yellow](all-in)[/]",
				_ => ""
			};

			table.AddRow(
				marker,
				player.Name,
				player.ChipStack.ToString(),
				player.CurrentBet > 0 ? player.CurrentBet.ToString() : "-",
				status,
				player.CardCount.ToString()
			);
		}

		AnsiConsole.Write(table);
	}

	private static async Task ShowHandResultsAsync(ApiClient apiClient, Guid gameId, List<(Guid id, string name)> playerIds)
	{
		Logger.Paragraph("Hand Results");

		var handState = await apiClient.GetCurrentHandAsync(gameId);
		if (handState == null)
		{
			AnsiConsole.MarkupLine("[yellow]Unable to retrieve hand results.[/]");
			return;
		}

		AnsiConsole.MarkupLine($"[green]Final Pot: {handState.Pot}[/]");
		AnsiConsole.MarkupLine($"[dim]Phase: {handState.Phase}[/]");

		// Check if we need to do a showdown
		var activePlayers = handState.Players.Where(p => p.Status != "Folded").ToList();
		
		if (activePlayers.Count == 1)
		{
			// Player won by fold - call showdown to process pot distribution
			var showdownResult = await apiClient.ShowdownAsync(gameId);
			if (showdownResult?.Success == true)
			{
				var winner = showdownResult.Results.FirstOrDefault(r => r.IsWinner);
				if (winner != null)
				{
					AnsiConsole.MarkupLine($"\n[green bold]{winner.PlayerName} wins {winner.Payout} chips![/]");
					AnsiConsole.MarkupLine($"[dim]{winner.HandDescription}[/]");
				}
			}
			else
			{
				var winner = activePlayers[0];
				AnsiConsole.MarkupLine($"\n[green bold]{winner.Name} wins the pot![/]");
			}
		}
		else if (activePlayers.Count > 1)
		{
			// Multiple players - actual showdown
			var showdownResult = await apiClient.ShowdownAsync(gameId);
			if (showdownResult?.Success == true)
			{
				AnsiConsole.MarkupLine("\n[bold]Showdown Results:[/]");

				var resultsTable = new Table();
				resultsTable.AddColumn("Player");
				resultsTable.AddColumn("Hand");
				resultsTable.AddColumn("Type");
				resultsTable.AddColumn("Payout");

				foreach (var result in showdownResult.Results.OrderByDescending(r => r.Payout))
				{
					var handStr = string.Join(" ", result.Hand);
					var payoutStr = result.Payout > 0
						? $"[green]+{result.Payout}[/]"
						: "-";
					var winMarker = result.IsWinner ? "[gold1]★[/] " : "";

					resultsTable.AddRow(
						$"{winMarker}{result.PlayerName}",
						handStr,
						result.HandDescription,
						payoutStr
					);
				}

				AnsiConsole.Write(resultsTable);

				var winners = showdownResult.Results.Where(r => r.IsWinner).ToList();
				if (winners.Count == 1)
				{
					AnsiConsole.MarkupLine($"\n[green bold]{winners[0].PlayerName} wins with {winners[0].HandDescription}![/]");
				}
				else if (winners.Count > 1)
				{
					var winnerNames = string.Join(", ", winners.Select(w => w.PlayerName));
					AnsiConsole.MarkupLine($"\n[green bold]Split pot between: {winnerNames}[/]");
				}
			}
			else
			{
				AnsiConsole.MarkupLine($"[red]Showdown failed: {showdownResult?.ErrorMessage ?? "Unknown error"}[/]");
			}
		}

		// Show updated chip counts after showdown
		var updatedGameState = await apiClient.GetGameStateAsync(gameId);
		if (updatedGameState != null)
		{
			AnsiConsole.MarkupLine("\n[bold]Chip Counts After Hand:[/]");
			var chipsTable = new Table();
			chipsTable.AddColumn("Player");
			chipsTable.AddColumn("Chips");

			foreach (var player in updatedGameState.Players.OrderByDescending(p => p.ChipStack))
			{
				chipsTable.AddRow(
					player.Name,
					player.ChipStack.ToString()
				);
			}

			AnsiConsole.Write(chipsTable);
		}
	}

	private static async Task DisplayFinalStandingsAsync(ApiClient apiClient, Guid gameId)
	{
		var gameState = await apiClient.GetGameStateAsync(gameId);
		if (gameState == null)
		{
			AnsiConsole.MarkupLine("[yellow]Unable to retrieve final standings.[/]");
			return;
		}

		AnsiConsole.MarkupLine("[bold]Final Standings:[/]");

		var table = new Table();
		table.AddColumn("Rank");
		table.AddColumn("Player");
		table.AddColumn("Chips");

		var rankedPlayers = gameState.Players.OrderByDescending(p => p.ChipStack).ToList();
		for (int i = 0; i < rankedPlayers.Count; i++)
		{
			var player = rankedPlayers[i];
			var rank = i == 0 ? "[gold1]1st[/]" : (i + 1).ToString();
			table.AddRow(
				rank,
				player.Name,
				player.ChipStack.ToString()
			);
		}

		AnsiConsole.Write(table);

		if (rankedPlayers.Count > 0)
		{
			AnsiConsole.MarkupLine($"\n[green bold]Winner: {rankedPlayers[0].Name} with {rankedPlayers[0].ChipStack} chips![/]");
		}
	}

	private static void DisplayGameState(GetGameStateResponse gameState)
	{
		var table = new Table();
		table.AddColumn("Player");
		table.AddColumn("Position");
		table.AddColumn("Chips");
		table.AddColumn("Status");

		foreach (var player in gameState.Players.OrderBy(p => p.Position))
		{
			table.AddRow(
				player.Name,
				player.Position.ToString(),
				player.ChipStack.ToString(),
				player.Status
			);
		}

		AnsiConsole.Write(table);
		AnsiConsole.MarkupLine($"[dim]Game Status: {gameState.Status}[/]");
	}
}
