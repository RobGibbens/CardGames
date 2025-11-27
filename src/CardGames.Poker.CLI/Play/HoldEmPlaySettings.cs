using Spectre.Console.Cli;
using System.ComponentModel;

namespace CardGames.Poker.CLI.Play;

internal class HoldEmPlaySettings : PlaySettings
{
    [Description("Small blind amount")]
    [CommandOption("-s|--small-blind")]
    public int SmallBlind { get; set; } = 5;

    [Description("Big blind amount")]
    [CommandOption("-b|--big-blind")]
    public int BigBlind { get; set; } = 10;
}
