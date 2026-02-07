using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.CollectAntes;

public class CollectAntesCommandHandler(CardsDbContext context)
	: IRequestHandler<CollectAntesCommand, OneOf<CollectAntesSuccessful, CollectAntesError>>
{
	public async Task<OneOf<CollectAntesSuccessful, CollectAntesError>> Handle(
		CollectAntesCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new CollectAntesError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = CollectAntesErrorCode.GameNotFound
			};
		}

		if (game.CurrentPhase != nameof(Phases.CollectingAntes))
		{
			return new CollectAntesError
			{
				Message = $"Cannot collect antes. Game is in '{game.CurrentPhase}' phase. " +
						  $"Antes can only be collected when the game is in '{nameof(Phases.CollectingAntes)}' phase.",
				Code = CollectAntesErrorCode.InvalidGameState
			};
		}

		var mainPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
									  p.HandNumber == game.CurrentHandNumber &&
									  p.PotType == PotType.Main,
				cancellationToken);

		if (mainPot is null)
		{
			mainPot = new Pot
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber,
				PotType = PotType.Main,
				PotOrder = 0,
				Amount = 0,
				IsAwarded = false,
				CreatedAt = now
			};
			context.Pots.Add(mainPot);
		}

		var anteContributions = new List<AnteContribution>();
		var totalCollected = 0;
		var gameAnte = game.Ante ?? 0;

		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		foreach (var gamePlayer in activePlayers)
		{
			var anteAmount = Math.Min(gameAnte, gamePlayer.ChipStack);

			if (anteAmount > 0)
			{
				gamePlayer.ChipStack -= anteAmount;
				gamePlayer.CurrentBet = anteAmount;
				gamePlayer.TotalContributedThisHand += anteAmount;

				var wentAllIn = gamePlayer.ChipStack == 0;
				if (wentAllIn)
				{
					gamePlayer.IsAllIn = true;
				}

				mainPot.Amount += anteAmount;
				totalCollected += anteAmount;

				var contribution = new PotContribution
				{
					PotId = mainPot.Id,
					GamePlayerId = gamePlayer.Id,
					Amount = anteAmount,
					IsEligibleToWin = true,
					IsPotMatch = false,
					ContributedAt = now
				};
				context.PotContributions.Add(contribution);

				anteContributions.Add(new AnteContribution
				{
					PlayerName = gamePlayer.Player.Name,
					Amount = anteAmount,
					RemainingChips = gamePlayer.ChipStack,
					WentAllIn = wentAllIn
				});
			}
		}

		game.CurrentPhase = nameof(Phases.ThirdStreet);
		game.UpdatedAt = now;

		foreach (var gamePlayer in game.GamePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		await context.SaveChangesAsync(cancellationToken);

		return new CollectAntesSuccessful
		{
			GameId = game.Id,
			TotalAntesCollected = totalCollected,
			CurrentPhase = game.CurrentPhase,
			AnteContributions = anteContributions
		};
	}
}
