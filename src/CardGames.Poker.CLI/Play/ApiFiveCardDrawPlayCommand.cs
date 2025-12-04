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

	protected override async Task<int> ExecuteAsync(CommandContext context, ApiPlaySettings settings, CancellationToken cancellationToken)
	{
		Logger.LogApplicationStart();

		var apiUrl = settings.ApiUrl ?? "https://localhost:7034";
		using var apiClient = new ApiClient(apiUrl);

		try
		{
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

			AnsiConsole.Status()
				.Start("Creating game...", _ =>
				{
					// Synchronous wrapper for async call
				});

			var createResponse = await apiClient.CreateGameAsync(createRequest);
			if (createResponse == null)
			{
				AnsiConsole.MarkupLine("[red]Failed to create game.[/]");
				return 1;
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
				return 1;
			}

			var playerIds = new List<(Guid id, string name)>();

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
					return 1;
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
				return 1;
			}

			DisplayGameState(gameState);

			// Phase 1 ends here - gameplay will be added in Phase 2
			AnsiConsole.MarkupLine("\n[yellow]Phase 1 Complete - Game is ready to start.[/]");
			AnsiConsole.MarkupLine("[dim]Start Hand, Betting, and Showdown will be available in Phase 2.[/]");

			return 0;
		}
		catch (HttpRequestException ex)
		{
			AnsiConsole.MarkupLine($"[red]API Error: {ex.Message}[/]");
			AnsiConsole.MarkupLine("[dim]Make sure the API server is running.[/]");
			return 1;
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