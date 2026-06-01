using CardGames.Poker.Api.Contracts;
using FluentValidation;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateFavoriteVariants;

public sealed class UpdateFavoriteVariantsRequestValidator : AbstractValidator<UpdateFavoriteVariantsRequest>
{
    public UpdateFavoriteVariantsRequestValidator()
    {
        RuleFor(x => x.FavoriteVariantCodes)
            .NotNull()
            .Must(codes => codes.All(code => !string.IsNullOrWhiteSpace(code)))
            .WithMessage("Favorite variant codes cannot contain blank values.");
    }
}
