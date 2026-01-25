namespace CardGames.Contracts.AddChips;

/// <summary>
/// Successful response when chips are added.
/// </summary>
public sealed record AddChipsResponse
{
	/// <summary>
	/// The updated chip stack (may not include the added amount if queued).
	/// </summary>
	public required int NewChipStack { get; init; }

	/// <summary>
	/// The amount of pending chips waiting to be applied.
	/// </summary>
	public int PendingChipsToAdd { get; init; }

	/// <summary>
	/// Whether the chips were applied immediately.
	/// </summary>
	public bool AppliedImmediately { get; init; }

	/// <summary>
	/// Message explaining the result.
	/// </summary>
	public required string Message { get; init; }
}
