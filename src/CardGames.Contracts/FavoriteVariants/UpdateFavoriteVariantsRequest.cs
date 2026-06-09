using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Api.Contracts;

public sealed record UpdateFavoriteVariantsRequest : IValidatableObject
{
	public required IReadOnlyList<string> FavoriteVariantCodes { get; init; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		foreach (var favoriteVariantCode in FavoriteVariantCodes)
			if (string.IsNullOrWhiteSpace(favoriteVariantCode))
				yield return new ValidationResult(
					"Favorite variant codes cannot contain blank values.",
					[nameof(FavoriteVariantCodes)]);
	}
}