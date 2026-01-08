using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CreateGame;
using CardGames.Poker.Games.SevenCardStud;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CollectAntes;

/// <summary>
/// Handles the <see cref="CollectAntesCommand"/> to collect mandatory ante bets from all players.
/// </summary>
public class CollectAntesCommandHandler(CardsDbContext context)
	: IRequestHandler<CollectAntesCommand, OneOf<CollectAntesSuccessful, CollectAntesError>>
{
	/// <inheritdoc />
	public async Task<OneOf<CollectAntesSuccessful, CollectAntesError>> Handle(
		CollectAntesCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players
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

		// 2. Validate game state allows collecting antes
		if (game.CurrentPhase != nameof(SevenCardStudPhase.CollectingAntes))
		{
			return new CollectAntesError
			{
				Message = $"Cannot collect antes. Game is in '{game.CurrentPhase}' phase. " +
						  $"Antes can only be collected when the game is in '{nameof(SevenCardStudPhase.CollectingAntes)}' phase.",
				Code = CollectAntesErrorCode.InvalidGameState
			};
		}

		// 3. Get the main pot for this hand
		var mainPot = await context.Pots
			.FirstOrDefaultAsync(p => p.GameId == game.Id &&
									  p.HandNumber == game.CurrentHandNumber &&
									  p.PotType == PotType.Main,
				cancellationToken);

		if (mainPot is null)
		{
			// Create main pot if it doesn't exist (shouldn't normally happen)
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

		// 4. Collect antes from each active player (mirrors SevenCardStudGame.CollectAntes)
		var anteContributions = new List<AnteContribution>();
		var totalCollected = 0;
		var gameAnte = game.Ante ?? 0;

		var activePlayers = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
			.OrderBy(gp => gp.SeatPosition)
			.ToList();

		foreach (var gamePlayer in activePlayers)
		{
			// Calculate ante amount (may be less if player is short-stacked)
			var anteAmount = Math.Min(gameAnte, gamePlayer.ChipStack);

			if (anteAmount > 0)
			{
				// Deduct from player's chip stack
				gamePlayer.ChipStack -= anteAmount;
				gamePlayer.CurrentBet = anteAmount;
				gamePlayer.TotalContributedThisHand += anteAmount;

				// Check if player is now all-in
				var wentAllIn = gamePlayer.ChipStack == 0;
				if (wentAllIn)
				{
					gamePlayer.IsAllIn = true;
				}

				// Add to pot
				mainPot.Amount += anteAmount;
				totalCollected += anteAmount;

				// Record pot contribution
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

				// Track contribution for response
				anteContributions.Add(new AnteContribution
				{
					PlayerName = gamePlayer.Player.Name,
					Amount = anteAmount,
					RemainingChips = gamePlayer.ChipStack,
					WentAllIn = wentAllIn
				});
			}
		}

		// 5. Update game state - transition to ThirdStreet phase
		game.CurrentPhase = nameof(SevenCardStudPhase.ThirdStreet);
		game.UpdatedAt = now;

		// 6. Reset current bets for all players (antes don't count toward betting rounds)
		foreach (var gamePlayer in game.GamePlayers)
		{
			gamePlayer.CurrentBet = 0;
		}

		// 7. Persist changes
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
