using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Klondike;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
///     Game flow handler for Klondike Hold'em poker.
///     Uses Hold 'Em dealing and betting flow. The Klondike Card (face-down wild card)
///     is dealt immediately after hole cards, before the first betting round.
/// </summary>
public sealed class KlondikeFlowHandler : BaseGameFlowHandler
{
	public override string GameTypeCode => "KLONDIKE";

	public override bool SkipsAnteCollection => true;

	public override GameRules GetGameRules()
	{
		return KlondikeRules.CreateGameRules();
	}

	public override DealingConfiguration GetDealingConfiguration()
	{
		return new DealingConfiguration
		{
			PatternType = DealingPatternType.CommunityCard,
			InitialCardsPerPlayer = 2,
			AllFaceDown = true
		};
	}

	public override string GetInitialPhase(Game game)
	{
		return "CollectingBlinds";
	}

	public override async Task DealCardsAsync(
		CardsDbContext context,
		Game game,
		List<GamePlayer> eligiblePlayers,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		await CollectBlindsAsync(context, game, eligiblePlayers, now, cancellationToken);
		await DealDrawStyleCardsAsync(context, game, eligiblePlayers, now, cancellationToken);

		// Deal the Klondike Card face-down as a community card immediately after hole cards.
		var klondikeCard = await context.GameCards
			.Where(gc => gc.GameId == game.Id
			             && gc.HandNumber == game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.FirstOrDefaultAsync(cancellationToken);

		if (klondikeCard is not null)
		{
			klondikeCard.Location = CardLocation.Community;
			klondikeCard.GamePlayerId = null;
			klondikeCard.IsVisible = false;
			klondikeCard.DealtAtPhase = "KlondikeCard";
			klondikeCard.DealOrder = 0;
			klondikeCard.DealtAt = now;
			await context.SaveChangesAsync(cancellationToken);
		}
	}

	protected override async Task SendBettingActionAsync(AutoActionContext context, BettingActionType action,
		int amount = 0)
	{
		var command = new ProcessBettingActionCommand(
			context.GameId, action, amount)
		{
			BoundSeat = context.PlayerSeatIndex
		};

		try
		{
			await context.Mediator.Send(command, context.CancellationToken);
		}
		catch (Exception ex)
		{
			context.Logger.LogError(ex, "Error performing auto-betting action for Klondike game {GameId}",
				context.GameId);
		}
	}
}