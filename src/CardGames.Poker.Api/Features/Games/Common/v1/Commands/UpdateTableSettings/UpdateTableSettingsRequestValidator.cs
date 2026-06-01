using FluentValidation;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.UpdateTableSettings;

public sealed class UpdateTableSettingsRequestValidator : AbstractValidator<UpdateTableSettingsRequest>
{
    public UpdateTableSettingsRequestValidator()
    {
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!)
                .MaximumLength(200);
        });

        When(x => x.Ante.HasValue, () =>
        {
            RuleFor(x => x.Ante.Value)
                .InclusiveBetween(0, 10_000);
        });

        When(x => x.MinBet.HasValue, () =>
        {
            RuleFor(x => x.MinBet.Value)
                .InclusiveBetween(1, 100_000);
        });

        When(x => x.SmallBlind.HasValue, () =>
        {
            RuleFor(x => x.SmallBlind.Value)
                .InclusiveBetween(1, 50_000);
        });

        When(x => x.BigBlind.HasValue, () =>
        {
            RuleFor(x => x.BigBlind.Value)
                .InclusiveBetween(1, 100_000);
        });

        RuleFor(x => x.RowVersion)
            .NotEmpty();
    }
}
