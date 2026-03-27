using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateFavoriteVariants;

public sealed record UpdateFavoriteVariantsCommand(IReadOnlyList<string> FavoriteVariantCodes)
	: IRequest<OneOf<FavoriteVariantsDto, UpdateFavoriteVariantsError>>;

public enum UpdateFavoriteVariantsErrorCode
{
	Unauthorized,
	InvalidFavoriteVariants
}

public sealed record UpdateFavoriteVariantsError(UpdateFavoriteVariantsErrorCode Code, string Message);