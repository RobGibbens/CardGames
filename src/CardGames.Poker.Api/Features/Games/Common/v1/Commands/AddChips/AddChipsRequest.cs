using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
/// Request to add chips to a player's stack.
/// </summary>
public sealed record AddChipsRequest
{
	/// <summary>
	/// The amount of chips to add (must be positive).
	/// </summary>
	[Range(1, int.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
	public required int Amount { get; init; }
}
