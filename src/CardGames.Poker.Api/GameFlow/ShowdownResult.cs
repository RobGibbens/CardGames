namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Result of a showdown operation performed by a game flow handler.
/// </summary>
public sealed class ShowdownResult
{
    /// <summary>
    /// Gets whether the showdown was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the winning player IDs (may be multiple for split pots).
    /// </summary>
    public required IReadOnlyList<Guid> WinnerPlayerIds { get; init; }

    /// <summary>
    /// Gets the player IDs of losers (for pot matching games).
    /// </summary>
    public required IReadOnlyList<Guid> LoserPlayerIds { get; init; }

    /// <summary>
    /// Gets the total pot amount that was awarded.
    /// </summary>
    public required int TotalPotAwarded { get; init; }

    /// <summary>
    /// Gets the winning hand description.
    /// </summary>
    public string? WinningHandDescription { get; init; }

    /// <summary>
    /// Gets whether the win was by fold (all other players folded).
    /// </summary>
    public bool WonByFold { get; init; }

    /// <summary>
    /// Gets any error message if the showdown failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful showdown result.
    /// </summary>
    /// <param name="winners">The list of winner player IDs.</param>
    /// <param name="losers">The list of loser player IDs.</param>
    /// <param name="potAwarded">The total pot amount awarded.</param>
    /// <param name="handDescription">Optional winning hand description.</param>
    /// <param name="wonByFold">Whether the win was by fold.</param>
    /// <returns>A successful showdown result.</returns>
    public static ShowdownResult Success(
        IReadOnlyList<Guid> winners,
        IReadOnlyList<Guid> losers,
        int potAwarded,
        string? handDescription = null,
        bool wonByFold = false) => new()
        {
            IsSuccess = true,
            WinnerPlayerIds = winners,
            LoserPlayerIds = losers,
            TotalPotAwarded = potAwarded,
            WinningHandDescription = handDescription,
            WonByFold = wonByFold
        };

    /// <summary>
    /// Creates a failed showdown result.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A failed showdown result.</returns>
    public static ShowdownResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        WinnerPlayerIds = [],
        LoserPlayerIds = [],
        TotalPotAwarded = 0,
        ErrorMessage = errorMessage
    };
}
