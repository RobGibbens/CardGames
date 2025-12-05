using FluentValidation;

namespace CardGames.Poker.Api.Features.Games.DrawCards;

/// <summary>
/// Validates draw cards requests.
/// </summary>
public class DrawCardsValidator : AbstractValidator<DrawCardsRequest>
{
	public DrawCardsValidator()
	{
		RuleFor(x => x.PlayerId)
			.NotEmpty()
			.WithMessage("Player ID is required.");

		RuleFor(x => x.DiscardIndices)
			.NotNull()
			.WithMessage("Discard indices list is required.");

		RuleFor(x => x.DiscardIndices.Count)
			.LessThanOrEqualTo(3)
			.When(x => x.DiscardIndices != null)
			.WithMessage("Cannot discard more than 3 cards.");

		RuleForEach(x => x.DiscardIndices)
			.InclusiveBetween(0, 4)
			.WithMessage("Card index must be between 0 and 4.");
	}
}
