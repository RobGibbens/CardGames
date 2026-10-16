using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record RenameCardDeckCommand(Guid DeckId, string Name)
	: IRequest<OneOf<CardDeckDetailDto, CardDeckError>>;

public sealed class RenameCardDeckCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	ICardDeckStorageService storage)
	: IRequestHandler<RenameCardDeckCommand, OneOf<CardDeckDetailDto, CardDeckError>>
{
	public async Task<OneOf<CardDeckDetailDto, CardDeckError>> Handle(RenameCardDeckCommand request,
		CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return new CardDeckError(CardDeckErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (!CardDeckSupport.TryNormalizeName(request.Name, out var name, out var nameError))
		{
			return new CardDeckError(CardDeckErrorCode.InvalidName, nameError!);
		}

		var deck = await context.UserCardDecks
			.Include(candidate => candidate.Images)
			.FirstOrDefaultAsync(candidate => candidate.Id == request.DeckId && candidate.UserId == userId,
				cancellationToken);
		if (deck is null)
		{
			return new CardDeckError(CardDeckErrorCode.NotFound, "Deck was not found.");
		}

		var duplicate = await context.UserCardDecks.AnyAsync(
			candidate => candidate.UserId == userId && candidate.Id != deck.Id && candidate.Name.ToLower() == name.ToLower(),
			cancellationToken);
		if (duplicate)
		{
			return new CardDeckError(CardDeckErrorCode.DuplicateName, "You already have a deck with that name.");
		}

		deck.Name = name;
		deck.UpdatedAtUtc = DateTimeOffset.UtcNow;
		await context.SaveChangesAsync(cancellationToken);
		return CardDeckSupport.ToDetail(deck, storage);
	}
}
