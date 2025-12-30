using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.CreateGame;
using CardGames.Poker.Games.FiveCardDraw;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.StartHand;

/// <summary>
/// Handles the <see cref="StartHandCommand"/> to start a new hand in a Five Card Draw game.
/// </summary>
public class StartHandCommandHandler(CardsDbContext context)
	: IRequestHandler<StartHandCommand, OneOf<StartHandSuccessful, StartHandError>>
{
	public async Task<OneOf<StartHandSuccessful, StartHandError>> Handle(
		StartHandCommand command,
		CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// 1. Load the game with its players
		var game = await context.Games
			.Include(g => g.GamePlayers)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new StartHandError
			{
				Message = $"Game with ID '{command.GameId}' was not found.",
				Code = StartHandErrorCode.GameNotFound
			};
		}

		// 2. Validate game state allows starting a new hand
		var validPhases = new[]
		{
			nameof(FiveCardDrawPhase.WaitingToStart),
			nameof(FiveCardDrawPhase.Complete)
		};

				if (!validPhases.Contains(game.CurrentPhase))
				{
					return new StartHandError
					{
						Message = $"Cannot start a new hand. Game is in '{game.CurrentPhase}' phase. " +
								  $"A new hand can only be started when the game is in '{nameof(FiveCardDrawPhase.WaitingToStart)}' " +
								  $"or '{nameof(FiveCardDrawPhase.Complete)}' phase.",
						Code = StartHandErrorCode.InvalidGameState
					};
				}

				// 3. Auto-sit-out players with insufficient chips for the ante
				var ante = game.Ante ?? 0;
				var playersWithInsufficientChips = game.GamePlayers
					.Where(gp => gp.Status == GamePlayerStatus.Active &&
								 !gp.IsSittingOut &&
								 gp.ChipStack > 0 &&
								 gp.ChipStack < ante)
					.ToList();

				foreach (var player in playersWithInsufficientChips)
				{
					player.IsSittingOut = true;
				}

				// 4. Get eligible players (active, not sitting out, chips >= ante or ante is 0)
				var eligiblePlayers = game.GamePlayers
					.Where(gp => gp.Status == GamePlayerStatus.Active &&
								 !gp.IsSittingOut &&
								 (ante == 0 || gp.ChipStack >= ante))
					.ToList();

				if (eligiblePlayers.Count < 2)
				{
					return new StartHandError
					{
						Message = "Not enough eligible players to start a new hand. At least 2 players with sufficient chips are required.",
						Code = StartHandErrorCode.NotEnoughPlayers
					};
				}

				// 5. Reset player states for new hand (mirrors FiveCardDrawGame.StartHand)
				foreach (var gamePlayer in eligiblePlayers)
				{
					gamePlayer.CurrentBet = 0;
					gamePlayer.TotalContributedThisHand = 0;
					gamePlayer.HasFolded = false;
					gamePlayer.IsAllIn = false;
					gamePlayer.HasDrawnThisRound = false;
				}

				// 6. Remove any existing cards from previous hand
				var existingCards = await context.GameCards
					.Where(gc => gc.GameId == game.Id)
					.ToListAsync(cancellationToken);

				if (existingCards.Count > 0)
				{
					context.GameCards.RemoveRange(existingCards);
				}

				// 7. Create a new main pot for this hand
				var mainPot = new Pot
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber + 1,
					PotType = PotType.Main,
					PotOrder = 0,
					Amount = 0,
					IsAwarded = false,
					CreatedAt = now
				};

				context.Pots.Add(mainPot);

				// 8. Update game state
				game.CurrentHandNumber++;
				game.CurrentPhase = nameof(FiveCardDrawPhase.CollectingAntes);
				game.Status = GameStatus.InProgress;
				game.CurrentPlayerIndex = -1;
				game.CurrentDrawPlayerIndex = -1;
				game.HandCompletedAt = null;
				game.NextHandStartsAt = null;
				game.UpdatedAt = now;

				// Set StartedAt only on first hand
				game.StartedAt ??= now;

				// 9. Persist changes
				await context.SaveChangesAsync(cancellationToken);

				return new StartHandSuccessful
				{
					GameId = game.Id,
					HandNumber = game.CurrentHandNumber,
					CurrentPhase = game.CurrentPhase,
					ActivePlayerCount = eligiblePlayers.Count
				};
			}
		}
