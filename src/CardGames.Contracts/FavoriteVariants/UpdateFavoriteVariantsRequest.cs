using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Api.Contracts;

public sealed record UpdateFavoriteVariantsRequest : IValidatableObject
{
	public required IReadOnlyList<string> FavoriteVariantCodes { get; init; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		for (var index = 0; index < FavoriteVariantCodes.Count; index++)
		{
			if (string.IsNullOrWhiteSpace(FavoriteVariantCodes[index]))
			{
				yield return new ValidationResult(
					"Favorite variant codes cannot contain blank values.",
					[nameof(FavoriteVariantCodes)]);
			}
		}
	}
}