using System.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.Telemetry;

public static class PokerActivitySource
{
    public const string Name = "CardGames.Poker.Api";

    public static readonly ActivitySource Source = new(Name);
}
