using Spectre.Console.Cli;
using System.ComponentModel;

namespace CardGames.Poker.CLI.Play;

internal class FollowTheQueenPlaySettings : PlaySettings
{
    [Description("Bring-in amount (typically half the small bet)")]
    [CommandOption("-b|--bring-in")]
    public int BringIn { get; set; } = 5;

    [Description("Small bet amount (used on 3rd and 4th street)")]
    [CommandOption("-s|--small-bet")]
    public int SmallBet { get; set; } = 10;

    [Description("Big bet amount (used on 5th, 6th, and 7th street)")]
    [CommandOption("-g|--big-bet")]
    public int BigBet { get; set; } = 20;
}
