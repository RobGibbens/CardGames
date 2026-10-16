using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record SelectCardDeckCommand(Guid? DeckId) : IRequest<OneOf<CardDeckListDto, CardDeckError>>;

public sealed class SelectCardDeckCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUser,
	IMediator mediator)
	: IRequestHandler<SelectCardDeckCommand, OneOf<CardDeckListDto, CardDeckError>>
{
	public async Task<OneOf<CardDeckListDto, CardDeckError>> Handle(SelectCardDeckCommand request,
		CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return new CardDeckError(CardDeckErrorCode.Unauthorized, "User is not authenticated.");
		}

		var user = await context.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
		if (user is null)
		{
			return new CardDeckError(CardDeckErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (request.DeckId is null)
		{
			user.SelectedCardDeckId = null;
			await context.SaveChangesAsync(cancellationToken);
			return await mediator.Send(new ListCardDecksQuery(), cancellationToken);
		}

		var deck = await context.UserCardDecks
			.Include(candidate => candidate.Images)
			.FirstOrDefaultAsync(candidate => candidate.Id == request.DeckId && candidate.UserId == userId,
				cancellationToken);
		if (deck is null)
		{
			return new CardDeckError(CardDeckErrorCode.NotFound, "Deck was not found.");
		}

		if (!CardDeckSupport.IsComplete(deck.Images))
		{
			return new CardDeckError(CardDeckErrorCode.Incomplete,
				"Upload the back and all 52 card fronts before using this deck.");
		}

		user.SelectedCardDeckId = deck.Id;
		await context.SaveChangesAsync(cancellationToken);
		return await mediator.Send(new ListCardDecksQuery(), cancellationToken);
	}
}
