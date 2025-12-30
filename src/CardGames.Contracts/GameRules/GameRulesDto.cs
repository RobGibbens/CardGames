namespace CardGames.Contracts.GameRules;

/// <summary>
/// DTO for game rules metadata.
/// Describes the flow, phases, and mechanics of a poker game variant.
/// </summary>
public sealed record GameRulesDto
{
    /// <summary>
    /// Gets the unique code identifying this game variant.
    /// </summary>
    public required string GameTypeCode { get; init; }

    /// <summary>
    /// Gets the display name of the game variant.
    /// </summary>
    public required string GameTypeName { get; init; }

    /// <summary>
    /// Gets a description of the game variant.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the minimum number of players required.
    /// </summary>
    public required int MinPlayers { get; init; }

    /// <summary>
    /// Gets the maximum number of players supported.
    /// </summary>
    public required int MaxPlayers { get; init; }

    /// <summary>
    /// Gets the ordered list of phases that occur in a typical hand.
    /// </summary>
    public required IReadOnlyList<PhaseDescriptorDto> Phases { get; init; }

    /// <summary>
    /// Gets configuration for how cards are dealt in this game.
    /// </summary>
    public required CardDealingConfigDto CardDealing { get; init; }

    /// <summary>
    /// Gets configuration for betting in this game.
    /// </summary>
    public required BettingConfigDto Betting { get; init; }

    /// <summary>
    /// Gets configuration for drawing/discarding cards, if applicable.
    /// </summary>
    public DrawingConfigDto? Drawing { get; init; }

    /// <summary>
    /// Gets configuration for showdown behavior.
    /// </summary>
    public required ShowdownConfigDto Showdown { get; init; }

    /// <summary>
    /// Gets any special rules or features unique to this game.
    /// </summary>
    public IReadOnlyDictionary<string, object>? SpecialRules { get; init; }
}

/// <summary>
/// DTO for a phase descriptor.
/// </summary>
public sealed record PhaseDescriptorDto
{
    /// <summary>
    /// Gets the unique identifier for this phase.
    /// </summary>
    public required string PhaseId { get; init; }

    /// <summary>
    /// Gets the display name of this phase.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a description of what happens in this phase.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the category of this phase.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets whether this phase requires player action.
    /// </summary>
    public required bool RequiresPlayerAction { get; init; }

    /// <summary>
    /// Gets the actions available during this phase.
    /// </summary>
    public IReadOnlyList<string> AvailableActions { get; init; } = [];

    /// <summary>
    /// Gets whether this is a terminal phase.
    /// </summary>
    public bool IsTerminal { get; init; }
}

/// <summary>
/// DTO for card dealing configuration.
/// </summary>
public sealed record CardDealingConfigDto
{
    /// <summary>
    /// Gets the number of cards dealt to each player initially.
    /// </summary>
    public required int InitialCards { get; init; }

    /// <summary>
    /// Gets whether cards are dealt face up or face down.
    /// </summary>
    public required string InitialVisibility { get; init; }

    /// <summary>
    /// Gets whether the game uses community cards.
    /// </summary>
    public bool HasCommunityCards { get; init; }

    /// <summary>
    /// Gets the dealing pattern for games with mixed visibility.
    /// </summary>
    public IReadOnlyList<DealingRoundDto>? DealingRounds { get; init; }
}

/// <summary>
/// DTO for a dealing round.
/// </summary>
public sealed record DealingRoundDto
{
    /// <summary>
    /// Gets the number of cards to deal in this round.
    /// </summary>
    public required int CardCount { get; init; }

    /// <summary>
    /// Gets the visibility of the cards dealt in this round.
    /// </summary>
    public required string Visibility { get; init; }

    /// <summary>
    /// Gets whether this round deals to individual players or the board.
    /// </summary>
    public required string Target { get; init; }
}

/// <summary>
/// DTO for betting configuration.
/// </summary>
public sealed record BettingConfigDto
{
    /// <summary>
    /// Gets whether the game uses antes.
    /// </summary>
    public required bool HasAntes { get; init; }

    /// <summary>
    /// Gets whether the game uses blinds.
    /// </summary>
    public bool HasBlinds { get; init; }

    /// <summary>
    /// Gets the number of betting rounds in a typical hand.
    /// </summary>
    public required int BettingRounds { get; init; }

    /// <summary>
    /// Gets the betting structure.
    /// </summary>
    public required string Structure { get; init; }
}

/// <summary>
/// DTO for drawing configuration.
/// </summary>
public sealed record DrawingConfigDto
{
    /// <summary>
    /// Gets whether the game allows drawing cards.
    /// </summary>
    public required bool AllowsDrawing { get; init; }

    /// <summary>
    /// Gets the maximum number of cards that can be discarded.
    /// </summary>
    public int? MaxDiscards { get; init; }

    /// <summary>
    /// Gets special rules for discarding.
    /// </summary>
    public string? SpecialRules { get; init; }

    /// <summary>
    /// Gets the number of drawing rounds.
    /// </summary>
    public int DrawingRounds { get; init; } = 1;
}

/// <summary>
/// DTO for showdown configuration.
/// </summary>
public sealed record ShowdownConfigDto
{
    /// <summary>
    /// Gets the hand ranking system used.
    /// </summary>
    public required string HandRanking { get; init; }

    /// <summary>
    /// Gets whether the game uses high/low split.
    /// </summary>
    public bool IsHighLow { get; init; }

    /// <summary>
    /// Gets whether the game has special pot-splitting rules.
    /// </summary>
    public bool HasSpecialSplitRules { get; init; }

    /// <summary>
    /// Gets description of special split rules if applicable.
    /// </summary>
    public string? SpecialSplitDescription { get; init; }
}
