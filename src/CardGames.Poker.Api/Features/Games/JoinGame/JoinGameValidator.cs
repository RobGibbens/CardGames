using FluentValidation;

namespace CardGames.Poker.Api.Features.Games.JoinGame;

/// <summary>
/// Validates join game requests.
/// </summary>
public class JoinGameValidator : AbstractValidator<JoinGameRequest>
{
	public JoinGameValidator()
	{
		RuleFor(x => x.PlayerName)
			.NotEmpty()
			.WithMessage("Player name is required.")
			.MaximumLength(50)
			.WithMessage("Player name cannot exceed 50 characters.");

		RuleFor(x => x.BuyIn)
			.GreaterThan(0)
			.When(x => x.BuyIn.HasValue)
			.WithMessage("Buy-in must be greater than 0.");
	}
}