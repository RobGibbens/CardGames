using CardGames.Poker.Api.Contracts;
using FluentValidation;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateGamePreferences;

public sealed class UpdateGamePreferencesRequestValidator : AbstractValidator<UpdateGamePreferencesRequest>
{
    public UpdateGamePreferencesRequestValidator()
    {
        RuleFor(x => x.DefaultSmallBlind)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Small blind must be greater than or equal to 0.");

        RuleFor(x => x.DefaultBigBlind)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Big blind must be greater than or equal to 0.")
            .GreaterThanOrEqualTo(x => x.DefaultSmallBlind)
            .WithMessage("Big blind must be greater than or equal to small blind.");

        RuleFor(x => x.DefaultAnte)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Ante must be greater than or equal to 0.");

        RuleFor(x => x.DefaultMinimumBet)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Minimum bet must be greater than or equal to 0.");
    }
}
