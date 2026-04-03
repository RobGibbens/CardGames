using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SharedDealHandsCommand = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsCommand;
using SharedDealHandsError = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsError;
using SharedDealHandsSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsSuccessful;

using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.DealHands;

/// <summary>
/// Delegates to SevenCardStud DealHands, then places 2 Tollbooth display cards
/// on the table after ThirdStreet dealing.
/// </summary>
public sealed class DealHandsCommandHandler(
	IRequestHandler<SharedDealHandsCommand, OneOf<SharedDealHandsSuccessful, SharedDealHandsError>> innerHandler,
	CardsDbContext context,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager)
	: IRequestHandler<DealHandsCommand, OneOf<SharedDealHandsSuccessful, SharedDealHandsError>>
{
	public async Task<OneOf<SharedDealHandsSuccessful, SharedDealHandsError>> Handle(
		DealHandsCommand command,
		CancellationToken cancellationToken)
	{
		var result = await innerHandler.Handle(
			new SharedDealHandsCommand(command.GameId), cancellationToken);

		if (result.IsT1)
		{
			return result;
		}

		var success = result.AsT0;

		// After ThirdStreet dealing, place 2 display cards for the Tollbooth mechanic
		if (success.CurrentPhase == nameof(Phases.ThirdStreet))
		{
			var now = DateTimeOffset.UtcNow;

			var game = await context.Games.FindAsync([command.GameId], cancellationToken);
			if (game is not null)
			{
				var deckCards = await context.GameCards
					.Where(gc => gc.GameId == command.GameId &&
								 gc.HandNumber == game.CurrentHandNumber &&
								 gc.Location == CardLocation.Deck)
					.OrderBy(gc => gc.DealOrder)
					.Take(2)
					.ToListAsync(cancellationToken);

				if (deckCards.Count >= 2)
				{
					var maxDealOrder = await context.GameCards
						.Where(gc => gc.GameId == command.GameId &&
									 gc.HandNumber == game.CurrentHandNumber &&
									 gc.Location != CardLocation.Deck)
						.MaxAsync(gc => (int?)gc.DealOrder, cancellationToken) ?? 0;

					foreach (var card in deckCards)
					{
						card.Location = CardLocation.Community;
						card.DealOrder = ++maxDealOrder;
						card.IsVisible = true;
						card.DealtAt = now;
						card.DealtAtPhase = nameof(Phases.ThirdStreet);
					}

					TollboothVariantState.SetPreviousBettingStreet(game, nameof(Phases.ThirdStreet));
					await context.SaveChangesAsync(cancellationToken);

					if (engineOptions.Value.Enabled)
						await gameStateManager.ReloadGameAsync(command.GameId, cancellationToken);
				}
			}
		}

		return result;
	}
}
