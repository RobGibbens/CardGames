using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Poker.CLI.Play;

using Spectre.Console.Cli;
using System.ComponentModel;

/// <summary>
/// Settings for API-backed poker games.
/// </summary>
public class ApiPlaySettings : CommandSettings
{
	[CommandOption("-u|--api-url <URL>")]
	[Description("The base URL of the CardGames API")]
	public string? ApiUrl { get; init; }

	[CommandOption("-n|--players <COUNT>")]
	[Description("Number of players")]
	public int NumberOfPlayers { get; init; }

	[CommandOption("-a|--ante <AMOUNT>")]
	[Description("Ante amount")]
	public int Ante { get; init; }

	[CommandOption("-m|--min-bet <AMOUNT>")]
	[Description("Minimum bet amount")]
	public int MinBet { get; init; }

	[CommandOption("-c|--chips <AMOUNT>")]
	[Description("Starting chips per player")]
	public int StartingChips { get; init; }
}