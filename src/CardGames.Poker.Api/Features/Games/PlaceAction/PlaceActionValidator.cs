using CardGames.Poker.Betting;
using FluentValidation;

namespace CardGames.Poker.Api.Features.Games.PlaceAction;

public class PlaceActionValidator : AbstractValidator<PlaceActionRequest>
{
	public PlaceActionValidator()
	{
		RuleFor(x => x.PlayerId)
			.NotEmpty()
			.WithMessage("Player ID is required.");

		RuleFor(x => x.ActionType)
			.IsInEnum()
			.WithMessage("Invalid action type.");

		RuleFor(x => x.Amount)
			.GreaterThan(0)
			.When(x => x.ActionType is BettingActionType.Bet or BettingActionType.Raise)
			.WithMessage("Amount must be greater than 0 for Bet/Raise actions.");

		RuleFor(x => x.Amount)
			.Equal(0)
			.When(x => x.ActionType is BettingActionType.Check or BettingActionType.Fold)
			.WithMessage("Amount must be 0 for Check/Fold actions.");
	}
}
