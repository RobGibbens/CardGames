using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public sealed class ProcessBuyCardCommandHandler(CardsDbContext context)
	: IRequestHandler<ProcessBuyCardCommand, OneOf<ProcessBuyCardSuccessful, ProcessBuyCardError>>
{
	public async Task<OneOf<ProcessBuyCardSuccessful, ProcessBuyCardError>> Handle(
		ProcessBuyCardCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.Pots)
				.ThenInclude(p => p.Contributions)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new ProcessBuyCardError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = ProcessBuyCardErrorCode.GameNotFound
			};
		}

		if (!string.Equals(game.CurrentPhase, nameof(Phases.BuyCardOffer), StringComparison.OrdinalIgnoreCase))
		{
			return new ProcessBuyCardError
			{
				Message = $"Cannot process buy card. Game is in '{game.CurrentPhase}' phase.",
				Code = ProcessBuyCardErrorCode.InvalidGameState
			};
		}

		var player = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (player is null)
		{
			return new ProcessBuyCardError
			{
				Message = "Player not found in game.",
				Code = ProcessBuyCardErrorCode.PlayerNotFound
			};
		}

		var buyCardState = BaseballGameSettings.GetState(game, game.MinBet ?? 0);
		if (buyCardState.PendingOffers.Count == 0)
		{
			return new ProcessBuyCardError
			{
				Message = "No pending buy card offers.",
				Code = ProcessBuyCardErrorCode.NoPendingOffer
			};
		}

		var currentOffer = buyCardState.PendingOffers.FirstOrDefault(o => o.PlayerId == command.PlayerId);
		if (currentOffer is null)
		{
			return new ProcessBuyCardError
			{
				Message = "Player does not have a pending buy card offer.",
				Code = ProcessBuyCardErrorCode.NoPendingOffer
			};
		}

		var buyCardPrice = buyCardState.BuyCardPrice > 0 ? buyCardState.BuyCardPrice : (game.MinBet ?? 0);
		if (command.Accept && player.ChipStack < buyCardPrice)
		{
			return new ProcessBuyCardError
			{
				Message = "Insufficient chips to buy a card.",
				Code = ProcessBuyCardErrorCode.InsufficientChips
			};
		}

		if (command.Accept && buyCardPrice > 0)
		{
			player.ChipStack -= buyCardPrice;
			player.TotalContributedThisHand += buyCardPrice;

			if (player.ChipStack == 0)
			{
				player.IsAllIn = true;
			}

			var pot = game.Pots.FirstOrDefault(p => p.PotOrder == 0 && p.HandNumber == game.CurrentHandNumber);
			if (pot is null)
			{
				pot = new Pot
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					PotType = PotType.Main,
					PotOrder = 0,
					Amount = 0,
					IsAwarded = false,
					CreatedAt = now
				};
				context.Pots.Add(pot);
			}

			pot.Amount += buyCardPrice;

			var contribution = pot.Contributions.FirstOrDefault(c => c.GamePlayerId == player.Id);
			if (contribution is not null)
			{
				contribution.Amount += buyCardPrice;
			}
			else
			{
				context.PotContributions.Add(new PotContribution
				{
					PotId = pot.Id,
					GamePlayerId = player.Id,
					Amount = buyCardPrice,
					IsEligibleToWin = true,
					IsPotMatch = false,
					ContributedAt = now
				});
			}

			var deckCard = await context.GameCards
				.Where(gc => gc.GameId == game.Id &&
							 gc.HandNumber == game.CurrentHandNumber &&
							 gc.Location == CardLocation.Deck)
				.OrderBy(gc => gc.DealOrder)
				.FirstOrDefaultAsync(cancellationToken);

			if (deckCard is not null)
			{
				// Find the card that triggered the offer (the 4)
				var triggerCard = await context.GameCards
					.FirstOrDefaultAsync(c => c.Id == currentOffer.CardId, cancellationToken);

				int newDealOrder;
				if (triggerCard != null)
				{
					newDealOrder = triggerCard.DealOrder + 1;

					// Shift subsequent cards to make room for the new card
					var cardsToShift = await context.GameCards
						.Where(gc => gc.GameId == game.Id &&
									 gc.HandNumber == game.CurrentHandNumber &&
									 gc.GamePlayerId == player.Id &&
									 gc.DealOrder >= newDealOrder &&
									 gc.Location != CardLocation.Deck &&
									 !gc.IsDiscarded)
						.ToListAsync(cancellationToken);

					foreach (var card in cardsToShift)
					{
						card.DealOrder++;
					}
				}
				else
				{
					// Fallback: append to end
					var existingCardCount = await context.GameCards
						.CountAsync(gc => gc.GamePlayerId == player.Id &&
										  gc.HandNumber == game.CurrentHandNumber &&
										  gc.Location != CardLocation.Deck &&
										  !gc.IsDiscarded, cancellationToken);
					newDealOrder = existingCardCount + 1;
				}

				deckCard.GamePlayerId = player.Id;
				// Deal face up (Board) instead of face down (Hole)
				deckCard.Location = CardLocation.Board;
				deckCard.IsVisible = true;
				deckCard.DealtAtPhase = currentOffer.Street;
				deckCard.DealtAt = now;
				deckCard.IsBuyCard = true;
				deckCard.DealOrder = newDealOrder;
			}
		}

		var remainingOffers = buyCardState.PendingOffers
			.Where(o => o.PlayerId != command.PlayerId)
			.ToList();

		string? nextPhase = null;
		int nextActor = -1;

		if (remainingOffers.Count > 0)
		{
			var nextOffer = remainingOffers[0];
			nextPhase = nameof(Phases.BuyCardOffer);
			nextActor = nextOffer.SeatPosition;
		}
		else
		{
			nextPhase = buyCardState.ReturnPhase ?? nameof(Phases.ThirdStreet);
			nextActor = buyCardState.ReturnActorIndex ?? -1;
		}

		var updatedState = buyCardState with
		{
			BuyCardPrice = buyCardPrice,
			PendingOffers = remainingOffers,
			ReturnPhase = remainingOffers.Count == 0 ? null : buyCardState.ReturnPhase,
			ReturnActorIndex = remainingOffers.Count == 0 ? null : buyCardState.ReturnActorIndex
		};
		BaseballGameSettings.SaveState(game, updatedState);

		game.CurrentPhase = nextPhase;
		game.CurrentPlayerIndex = nextActor;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		return new ProcessBuyCardSuccessful
		{
			GameId = game.Id,
			PlayerId = player.PlayerId,
			Accepted = command.Accept,
			CurrentPhase = game.CurrentPhase
		};
	}
}
