using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Api.Contracts;

public sealed record UpdateGamePreferencesRequest : IValidatableObject
{
	[Range(0, int.MaxValue, ErrorMessage = "Small blind must be greater than or equal to 0.")]
	public required int DefaultSmallBlind { get; init; }

	[Range(0, int.MaxValue, ErrorMessage = "Big blind must be greater than or equal to 0.")]
	public required int DefaultBigBlind { get; init; }

	[Range(0, int.MaxValue, ErrorMessage = "Ante must be greater than or equal to 0.")]
	public required int DefaultAnte { get; init; }

	[Range(0, int.MaxValue, ErrorMessage = "Minimum bet must be greater than or equal to 0.")]
	public required int DefaultMinimumBet { get; init; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (DefaultBigBlind < DefaultSmallBlind)
		{
			yield return new ValidationResult(
				"Big blind must be greater than or equal to small blind.",
				[nameof(DefaultBigBlind), nameof(DefaultSmallBlind)]);
		}
	}
}
