using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record GetCardDeckQuery(Guid DeckId) : IRequest<OneOf<CardDeckDetailDto, CardDeckError>>;

public sealed class GetCardDeckQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	ICardDeckStorageService storage)
	: IRequestHandler<GetCardDeckQuery, OneOf<CardDeckDetailDto, CardDeckError>>
{
	public async Task<OneOf<CardDeckDetailDto, CardDeckError>> Handle(GetCardDeckQuery request,
		CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return new CardDeckError(CardDeckErrorCode.Unauthorized, "User is not authenticated.");
		}

		var deck = await context.UserCardDecks.AsNoTracking()
			.Include(candidate => candidate.Images)
			.FirstOrDefaultAsync(candidate => candidate.Id == request.DeckId && candidate.UserId == userId,
				cancellationToken);

		if (deck is null)
		{
			return new CardDeckError(CardDeckErrorCode.NotFound, "Deck was not found.");
		}

		return CardDeckSupport.ToDetail(deck, storage);
	}
}
