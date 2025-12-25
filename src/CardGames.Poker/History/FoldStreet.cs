namespace CardGames.Poker.History;

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
