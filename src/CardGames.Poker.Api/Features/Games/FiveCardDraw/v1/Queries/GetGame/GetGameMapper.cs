using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGameMapper
{
	public static GetGameResponse ToResponse(this Game model, int minimumNumberOfPlayers, int maximumNumberOfPlayers)
	{
		// This is a simplified logic. You may need to reconstruct FiveCardDrawGame from model for full logic.
		// Here, we use GamePlayers and chip stacks to determine CanContinue.
		var activePlayers = model.GamePlayers?.Count(gp => gp.ChipStack > 0) ?? 0;
		return new GetGameResponse(
			model.Id,
			model.GameTypeId,
			model.Name,
			minimumNumberOfPlayers,
			maximumNumberOfPlayers,
			model.CurrentPhase,
			model.CurrentHandNumber,
			model.DealerPosition,
			model.Ante,
			model.SmallBlind,
			model.BigBlind,
			model.BringIn,
			model.SmallBet,
			model.BigBet,
			model.MinBet,
			model.GameSettings,
			model.Status,
			model.CurrentPlayerIndex,
			model.BringInPlayerIndex,
			model.CurrentDrawPlayerIndex,
			model.RandomSeed,
			model.CreatedAt,
			model.UpdatedAt,
			model.StartedAt,
			model.EndedAt,
			activePlayers >= 2,
			MapRowVersion(model.RowVersion)
		);
	}

	public static IQueryable<GetGameResponse> ProjectToResponse(this IQueryable<Game> query, int minimumNumberOfPlayers, int maximumNumberOfPlayers)
	{
		return query.Select(model => ToResponse(model, minimumNumberOfPlayers, maximumNumberOfPlayers));
	}

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}
