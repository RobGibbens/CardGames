using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.CardDecks;

public sealed record ListCardDecksQuery : IRequest<CardDeckListDto>;

public sealed class ListCardDecksQueryHandler(CardsDbContext context, ICurrentUserService currentUser)
	: IRequestHandler<ListCardDecksQuery, CardDeckListDto>
{
	public async Task<CardDeckListDto> Handle(ListCardDecksQuery request, CancellationToken cancellationToken)
	{
		var userId = await CardDeckSupport.GetUserIdAsync(context, currentUser, cancellationToken);
		if (userId is null)
		{
			return new CardDeckListDto { Decks = BuiltInSummaries(null, customDeckSelected: false) };
		}

		var user = await context.Users.AsNoTracking()
			.Where(candidate => candidate.Id == userId)
			.Select(candidate => new { candidate.SelectedCardDeckId })
			.FirstOrDefaultAsync(cancellationToken);

		var decks = await context.UserCardDecks.AsNoTracking()
			.Include(deck => deck.Images)
			.Where(deck => deck.UserId == userId)
			.OrderBy(deck => deck.Name)
			.ToListAsync(cancellationToken);

		var selectedId = user?.SelectedCardDeckId;
		var selectedStillValid = selectedId is not null &&
		                         decks.Any(deck => deck.Id == selectedId && CardDeckSupport.IsComplete(deck.Images));

		var summaries = BuiltInSummaries(selectedId, selectedStillValid);
		summaries.AddRange(decks.Select(deck =>
			CardDeckSupport.ToSummary(deck, selectedStillValid && deck.Id == selectedId)));

		return new CardDeckListDto { Decks = summaries };
	}

	private static List<CardDeckSummaryDto> BuiltInSummaries(Guid? selectedId, bool customDeckSelected)
	{
		var active = customDeckSelected ? null : CardDeckSlots.ResolveBuiltIn(selectedId);
		return CardDeckSlots.BuiltIns
			.Select(deck => CardDeckSupport.BuiltInSummary(deck, active is not null && deck.Key == active.Key))
			.ToList();
	}
}
