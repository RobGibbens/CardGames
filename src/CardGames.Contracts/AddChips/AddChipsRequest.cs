namespace CardGames.Contracts.AddChips;

/// <summary>
/// Request to add chips to a player's stack.
/// </summary>
public sealed record AddChipsRequest
{
	/// <summary>
	/// The amount of chips to add.
	/// </summary>
	public required int Amount { get; init; }
}
