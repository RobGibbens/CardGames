using Spectre.Console.Cli;
using System.ComponentModel;

namespace CardGames.Poker.CLI.Play;

internal class PlaySettings : CommandSettings
{
    [Description("Number of players in the game")]
    [CommandOption("-p|--players")]
    public int NumberOfPlayers { get; set; }

    [Description("Starting chips for each player")]
    [CommandOption("-c|--chips")]
    public int StartingChips { get; set; } = 1000;

    [Description("Ante amount")]
    [CommandOption("-a|--ante")]
    public int Ante { get; set; } = 10;

    [Description("Minimum bet amount")]
    [CommandOption("-m|--min-bet")]
    public int MinBet { get; set; } = 20;
}