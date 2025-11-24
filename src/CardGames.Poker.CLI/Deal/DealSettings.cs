using Spectre.Console.Cli;
using System.ComponentModel;

namespace CardGames.Poker.CLI.Deal;

internal class DealSettings : CommandSettings
{
    [Description("Number of players in the game")]
    [CommandOption("-p|--players")]
    public int NumberOfPlayers { get; set; }
}
