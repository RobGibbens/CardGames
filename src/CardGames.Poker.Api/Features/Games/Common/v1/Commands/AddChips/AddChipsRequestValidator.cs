using CardGames.Contracts.AddChips;
using FluentValidation;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

public sealed class AddChipsRequestValidator : AbstractValidator<AddChipsRequest>
{
    public AddChipsRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");
    }
}
