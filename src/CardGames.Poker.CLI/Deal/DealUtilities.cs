using System.Collections.Generic;
using Spectre.Console;

namespace CardGames.Poker.CLI.Deal;

internal static class DealUtilities
{
    internal static List<string> GetPlayerNames(int numberOfPlayers)
    {
        var names = new List<string>();
        for (int i = 1; i <= numberOfPlayers; i++)
        {
            var name = AnsiConsole.Ask<string>($"Player {i} name: ");
            names.Add(name);
        }
        return names;
    }
}
