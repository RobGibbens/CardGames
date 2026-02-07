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

			context.PotContributions.Add(new PotContribution
			{
				PotId = pot.Id,
				GamePlayerId = player.Id,
				Amount = buyCardPrice,
				IsEligibleToWin = true,
				IsPotMatch = false,
				ContributedAt = now
			});

			var deckCard = await context.GameCards
				.Where(gc => gc.GameId == game.Id &&
							 gc.HandNumber == game.CurrentHandNumber &&
							 gc.Location == CardLocation.Deck)
				.OrderBy(gc => gc.DealOrder)
				.FirstOrDefaultAsync(cancellationToken);

			if (deckCard is not null)
			{
				var existingCardCount = await context.GameCards
					.CountAsync(gc => gc.GamePlayerId == player.Id &&
									  gc.HandNumber == game.CurrentHandNumber &&
									  gc.Location != CardLocation.Deck &&
									  !gc.IsDiscarded, cancellationToken);

				deckCard.GamePlayerId = player.Id;
				deckCard.Location = CardLocation.Hole;
				deckCard.IsVisible = false;
				deckCard.DealtAtPhase = currentOffer.Street;
				deckCard.DealtAt = now;
				deckCard.IsBuyCard = true;
				deckCard.DealOrder = existingCardCount + 1;
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
