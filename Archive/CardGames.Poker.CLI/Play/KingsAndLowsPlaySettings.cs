using Spectre.Console.Cli;
using System.ComponentModel;

namespace CardGames.Poker.CLI.Play;

internal class KingsAndLowsPlaySettings : PlaySettings
{
    [Description("Require King in hand to use low card as wild")]
    [CommandOption("-k|--king-required")]
    public bool KingRequired { get; set; } = false;

    [Description("Collect ante every hand (vs only at start of session)")]
    [CommandOption("-e|--ante-every-hand")]
    public bool AnteEveryHand { get; set; } = false;
}
