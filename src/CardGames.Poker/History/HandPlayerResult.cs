using System;

namespace CardGames.Poker.History;

/// <summary>
/// Records the end-of-hand outcome for a single player.
/// </summary>
/// <param name="PlayerId">The unique identifier of the player.</param>
/// <param name="PlayerName">The display name of the player at the time of the hand.</param>
/// <param name="ResultType">The final result category for this player.</param>
/// <param name="ReachedShowdown">Whether the player reached the showdown phase.</param>
/// <param name="FoldStreet">The street at which the player folded, if applicable.</param>
/// <param name="NetChipDelta">The net chip change for this player (positive = won, negative = lost).</param>
/// <param name="WentAllIn">Whether the player went all-in during this hand.</param>
/// <param name="AllInStreet">The street at which the player went all-in, if applicable.</param>
public sealed record HandPlayerResult(
    Guid PlayerId,
    string PlayerName,
    PlayerResultType ResultType,
    bool ReachedShowdown,
    FoldStreet? FoldStreet,
    int NetChipDelta,
    bool WentAllIn = false,
    FoldStreet? AllInStreet = null)
{
    /// <summary>
    /// Generates a UI-ready result label describing the player's outcome.
    /// </summary>
    /// <returns>A human-readable description such as "Folded (Turn)" or "Won (Showdown)".</returns>
    public string GetResultLabel()
    {
        return ResultType switch
        {
            PlayerResultType.Folded when FoldStreet.HasValue => $"Folded ({FormatStreet(FoldStreet.Value)})",
            PlayerResultType.Folded => "Folded",
            PlayerResultType.Won when ReachedShowdown => "Won",
            PlayerResultType.Won => "Won",
            PlayerResultType.SplitPotWon when ReachedShowdown => "Split Pot",
            PlayerResultType.SplitPotWon => "Split Pot",
            PlayerResultType.Lost when ReachedShowdown => "Lost",
            PlayerResultType.Lost => "Lost",
            _ => ResultType.ToString()
        };
    }

    private static string FormatStreet(FoldStreet street)
    {
        return street switch
        {
            History.FoldStreet.Preflop => "Preflop",
            History.FoldStreet.Flop => "Flop",
            History.FoldStreet.Turn => "Turn",
            History.FoldStreet.River => "River",
            History.FoldStreet.FirstRound => "1st Round",
            History.FoldStreet.DrawPhase => "Draw",
            History.FoldStreet.SecondRound => "2nd Round",
            History.FoldStreet.ThirdStreet => "3rd Street",
            History.FoldStreet.FourthStreet => "4th Street",
            History.FoldStreet.FifthStreet => "5th Street",
            History.FoldStreet.SixthStreet => "6th Street",
            History.FoldStreet.SeventhStreet => "7th Street",
            _ => street.ToString()
        };
    }
}
