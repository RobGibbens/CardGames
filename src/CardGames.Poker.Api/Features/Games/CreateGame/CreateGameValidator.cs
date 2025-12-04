using FluentValidation;

namespace CardGames.Poker.Api.Features.Games.CreateGame;

/// <summary>
/// Validates create game requests.
/// </summary>
public class CreateGameValidator : AbstractValidator<CreateGameRequest>
{
	public CreateGameValidator()
	{
		RuleFor(x => x.GameType)
			.IsInEnum()
			.WithMessage("Invalid game type specified.");

		When(x => x.Configuration != null, () =>
		{
			RuleFor(x => x.Configuration!.Ante)
				.GreaterThanOrEqualTo(0)
				.When(x => x.Configuration!.Ante.HasValue)
				.WithMessage("Ante must be non-negative.");

			RuleFor(x => x.Configuration!.MinBet)
				.GreaterThan(0)
				.When(x => x.Configuration!.MinBet.HasValue)
				.WithMessage("Minimum bet must be greater than 0.");

			RuleFor(x => x.Configuration!.StartingChips)
				.GreaterThan(0)
				.When(x => x.Configuration!.StartingChips.HasValue)
				.WithMessage("Starting chips must be greater than 0.");

			RuleFor(x => x.Configuration!.MaxPlayers)
				.InclusiveBetween(2, 6)
				.When(x => x.Configuration!.MaxPlayers.HasValue)
				.WithMessage("Max players must be between 2 and 6 for 5-Card Draw.");
		});
	}
}