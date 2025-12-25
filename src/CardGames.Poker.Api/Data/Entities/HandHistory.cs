namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Indicates how a poker hand terminated.
/// </summary>
public enum HandEndReason
{
    /// <summary>
    /// All other players folded, leaving one winner.
    /// </summary>
    FoldedToWinner = 0,

    /// <summary>
    /// Multiple players reached the showdown phase.
    /// </summary>
    Showdown = 1
}

/// <summary>
/// Indicates the final outcome for a player in a completed hand.
/// </summary>
public enum PlayerResultType
{
    /// <summary>
    /// Player folded before showdown.
    /// </summary>
    Folded = 0,

    /// <summary>
    /// Player won the hand (including via fold or showdown).
    /// </summary>
    Won = 1,

    /// <summary>
    /// Player lost at showdown.
    /// </summary>
    Lost = 2,

    /// <summary>
    /// Player won a split pot (tied with one or more other players).
    /// </summary>
    SplitPotWon = 3
}

/// <summary>
/// Indicates the betting street at which a player folded.
/// </summary>
public enum FoldStreet
{
    /// <summary>
    /// Player folded before any community cards were dealt.
    /// </summary>
    Preflop = 0,

    /// <summary>
    /// Player folded after the flop.
    /// </summary>
    Flop = 1,

    /// <summary>
    /// Player folded after the turn.
    /// </summary>
    Turn = 2,

    /// <summary>
    /// Player folded after the river.
    /// </summary>
    River = 3,

    /// <summary>
    /// Player folded during the first betting round (for draw games).
    /// </summary>
    FirstRound = 10,

    /// <summary>
    /// Player folded during the draw phase (for draw games).
    /// </summary>
    DrawPhase = 11,

    /// <summary>
    /// Player folded during the second betting round (for draw games).
    /// </summary>
    SecondRound = 12,

    /// <summary>
    /// Player folded during third street (for stud games).
    /// </summary>
    ThirdStreet = 20,

    /// <summary>
    /// Player folded during fourth street (for stud games).
    /// </summary>
    FourthStreet = 21,

    /// <summary>
    /// Player folded during fifth street (for stud games).
    /// </summary>
    FifthStreet = 22,

    /// <summary>
    /// Player folded during sixth street (for stud games).
    /// </summary>
    SixthStreet = 23,

    /// <summary>
    /// Player folded during seventh street (for stud games).
    /// </summary>
    SeventhStreet = 24
}

/// <summary>
/// Represents a complete summary of a poker hand upon completion.
/// This entity stores hand history for auditing and display purposes.
/// </summary>
public class HandHistory : EntityWithRowVersion
{
    /// <summary>
    /// The unique identifier for this hand history record.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The unique identifier of the game session this hand belongs to.
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Navigation property to the game.
    /// </summary>
    public Game Game { get; set; } = null!;

    /// <summary>
    /// The 1-based sequence number of this hand within the game.
    /// </summary>
    public int HandNumber { get; set; }

    /// <summary>
    /// The UTC timestamp when this hand completed.
    /// </summary>
    public DateTimeOffset CompletedAtUtc { get; set; }

    /// <summary>
    /// Indicates how the hand terminated.
    /// </summary>
    public HandEndReason EndReason { get; set; }

    /// <summary>
    /// The total pot size at settlement.
    /// </summary>
    public int TotalPot { get; set; }

    /// <summary>
    /// The rake amount taken from the pot, if applicable.
    /// </summary>
    public int Rake { get; set; }

    /// <summary>
    /// Optional description of the winning hand (e.g., "Full House, Aces over Kings").
    /// </summary>
    public string? WinningHandDescription { get; set; }

    /// <summary>
    /// Navigation property for the winners of this hand.
    /// </summary>
    public ICollection<HandHistoryWinner> Winners { get; set; } = [];

    /// <summary>
    /// Navigation property for all player results in this hand.
    /// </summary>
    public ICollection<HandHistoryPlayerResult> PlayerResults { get; set; } = [];
}

/// <summary>
/// Records a winner of a poker hand and the amount won.
/// </summary>
public class HandHistoryWinner
{
    /// <summary>
    /// The unique identifier for this winner record.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Foreign key to the hand history.
    /// </summary>
    public Guid HandHistoryId { get; set; }

    /// <summary>
    /// Navigation property to the hand history.
    /// </summary>
    public HandHistory HandHistory { get; set; } = null!;

    /// <summary>
    /// Foreign key to the player.
    /// </summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Navigation property to the player.
    /// </summary>
    public Player Player { get; set; } = null!;

    /// <summary>
    /// The display name of the player at the time of the hand.
    /// Stored for historical accuracy if player name changes later.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// The chip amount won by this player.
    /// </summary>
    public int AmountWon { get; set; }
}

/// <summary>
/// Records the end-of-hand outcome for a single player.
/// </summary>
public class HandHistoryPlayerResult
{
    /// <summary>
    /// The unique identifier for this player result record.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Foreign key to the hand history.
    /// </summary>
    public Guid HandHistoryId { get; set; }

    /// <summary>
    /// Navigation property to the hand history.
    /// </summary>
    public HandHistory HandHistory { get; set; } = null!;

    /// <summary>
    /// Foreign key to the player.
    /// </summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Navigation property to the player.
    /// </summary>
    public Player Player { get; set; } = null!;

    /// <summary>
    /// The display name of the player at the time of the hand.
    /// Stored for historical accuracy if player name changes later.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// The seat position of the player at the time of the hand.
    /// </summary>
    public int SeatPosition { get; set; }

    /// <summary>
    /// The final result category for this player.
    /// </summary>
    public PlayerResultType ResultType { get; set; }

    /// <summary>
    /// Whether the player reached the showdown phase.
    /// </summary>
    public bool ReachedShowdown { get; set; }

    /// <summary>
    /// The street at which the player folded, if applicable.
    /// </summary>
    public FoldStreet? FoldStreet { get; set; }

    /// <summary>
    /// The net chip change for this player (positive = won, negative = lost).
    /// </summary>
    public int NetChipDelta { get; set; }

    /// <summary>
    /// Whether the player went all-in during this hand.
    /// </summary>
    public bool WentAllIn { get; set; }

    /// <summary>
    /// The street at which the player went all-in, if applicable.
    /// </summary>
    public FoldStreet? AllInStreet { get; set; }

    /// <summary>
    /// Generates a UI-ready result label describing the player's outcome.
    /// </summary>
    public string GetResultLabel()
    {
        return ResultType switch
        {
            PlayerResultType.Folded when FoldStreet.HasValue => $"Folded ({FormatStreet(FoldStreet.Value)})",
            PlayerResultType.Folded => "Folded",
            PlayerResultType.Won when ReachedShowdown => "Won (Showdown)",
            PlayerResultType.Won => "Won (No Showdown)",
            PlayerResultType.SplitPotWon when ReachedShowdown => "Split Pot (Showdown)",
            PlayerResultType.SplitPotWon => "Split Pot",
            PlayerResultType.Lost when ReachedShowdown => "Lost (Showdown)",
            PlayerResultType.Lost => "Lost",
            _ => ResultType.ToString()
        };
    }

    private static string FormatStreet(FoldStreet street)
    {
        return street switch
        {
            Entities.FoldStreet.Preflop => "Preflop",
            Entities.FoldStreet.Flop => "Flop",
            Entities.FoldStreet.Turn => "Turn",
            Entities.FoldStreet.River => "River",
            Entities.FoldStreet.FirstRound => "1st Round",
            Entities.FoldStreet.DrawPhase => "Draw",
            Entities.FoldStreet.SecondRound => "2nd Round",
            Entities.FoldStreet.ThirdStreet => "3rd Street",
            Entities.FoldStreet.FourthStreet => "4th Street",
            Entities.FoldStreet.FifthStreet => "5th Street",
            Entities.FoldStreet.SixthStreet => "6th Street",
            Entities.FoldStreet.SeventhStreet => "7th Street",
            _ => street.ToString()
        };
    }
}
