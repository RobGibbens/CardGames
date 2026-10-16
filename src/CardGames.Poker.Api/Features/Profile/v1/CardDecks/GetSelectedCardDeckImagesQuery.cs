using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record GetSelectedCardDeckImagesQuery : IRequest<SelectedCardDeckImagesDto>;

public sealed class GetSelectedCardDeckImagesQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	ICardDeckStorageService storage)
	: IRequestHandler<GetSelectedCardDeckImagesQuery, SelectedCardDeckImagesDto>
{
	public async Task<SelectedCardDeckImagesDto> Handle(GetSelectedCardDeckImagesQuery request,
		CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return CardDeckSupport.BuiltInImages();
		}

		var selectedId = await context.Users.AsNoTracking()
			.Where(user => user.Id == userId)
			.Select(user => user.SelectedCardDeckId)
			.FirstOrDefaultAsync(cancellationToken);

		if (selectedId is null)
		{
			return CardDeckSupport.BuiltInImages();
		}

		if (CardDeckSlots.TryGetBuiltIn(selectedId.Value, out var builtIn))
		{
			return CardDeckSupport.BuiltInImages(builtIn);
		}

		var deck = await context.UserCardDecks.AsNoTracking()
			.Include(candidate => candidate.Images)
			.FirstOrDefaultAsync(candidate => candidate.Id == selectedId && candidate.UserId == userId,
				cancellationToken);

		if (deck is null || !CardDeckSupport.IsComplete(deck.Images))
		{
			return CardDeckSupport.BuiltInImages();
		}

		return CardDeckSupport.ToSelectedImages(deck, storage);
	}
}
