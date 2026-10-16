using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record DeleteCardDeckCommand(Guid DeckId) : IRequest<OneOf<Success, CardDeckError>>;

public readonly record struct Success;

public sealed class DeleteCardDeckCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	ICardDeckStorageService storage)
	: IRequestHandler<DeleteCardDeckCommand, OneOf<Success, CardDeckError>>
{
	public async Task<OneOf<Success, CardDeckError>> Handle(DeleteCardDeckCommand request,
		CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return new CardDeckError(CardDeckErrorCode.Unauthorized, "User is not authenticated.");
		}

		var deck = await context.UserCardDecks
			.FirstOrDefaultAsync(candidate => candidate.Id == request.DeckId && candidate.UserId == userId,
				cancellationToken);
		if (deck is null)
		{
			return new CardDeckError(CardDeckErrorCode.NotFound, "Deck was not found.");
		}

		var user = await context.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
		if (user?.SelectedCardDeckId == deck.Id)
		{
			user.SelectedCardDeckId = null;
		}

		context.UserCardDecks.Remove(deck);
		await context.SaveChangesAsync(cancellationToken);
		await storage.DeletePrefixAsync($"{userId}/{deck.Id}/", cancellationToken);
		return default(Success);
	}
}
