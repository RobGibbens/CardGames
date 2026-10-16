using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record CreateCardDeckCommand(string Name) : IRequest<OneOf<CardDeckDetailDto, CardDeckError>>;

public sealed class CreateCardDeckCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	ICardDeckStorageService storage)
	: IRequestHandler<CreateCardDeckCommand, OneOf<CardDeckDetailDto, CardDeckError>>
{
	public async Task<OneOf<CardDeckDetailDto, CardDeckError>> Handle(CreateCardDeckCommand request,
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

		var existingCount = await context.UserCardDecks.CountAsync(deck => deck.UserId == userId, cancellationToken);
		if (existingCount >= CardDeckSlots.MaxDecksPerUser)
		{
			return new CardDeckError(CardDeckErrorCode.LimitReached, "You can save up to 10 custom decks.");
		}

		var duplicate = await context.UserCardDecks.AnyAsync(
			deck => deck.UserId == userId && deck.Name.ToLower() == name.ToLower(), cancellationToken);
		if (duplicate)
		{
			return new CardDeckError(CardDeckErrorCode.DuplicateName, "You already have a deck with that name.");
		}

		var now = DateTimeOffset.UtcNow;
		var deck = new UserCardDeck
		{
			Id = Guid.NewGuid(),
			UserId = userId,
			Name = name,
			CreatedAtUtc = now,
			UpdatedAtUtc = now
		};
		context.UserCardDecks.Add(deck);
		await context.SaveChangesAsync(cancellationToken);
		return CardDeckSupport.ToDetail(deck, storage);
	}
}
