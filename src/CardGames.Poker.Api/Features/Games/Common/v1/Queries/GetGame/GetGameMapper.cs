using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGameMapper
{
	public static GetGameResponse ToResponse(this Game model, int minimumNumberOfPlayers, int maximumNumberOfPlayers)
	{
		// This is a simplified logic. You may need to reconstruct the game from model for full logic.
		// Here, we use GamePlayers and chip stacks to determine CanContinue.
		var activePlayers = model.GamePlayers?.Count(gp => gp.ChipStack > 0) ?? 0;
		return new GetGameResponse
		{
			Id = model.Id,
			GameTypeId = model.GameTypeId,
			GameTypeCode = model.GameType?.Code,
			GameTypeName = model.GameType?.Name,
			Name = model.Name,
			MinimumNumberOfPlayers = minimumNumberOfPlayers,
			MaximumNumberOfPlayers = maximumNumberOfPlayers,
			CurrentPhase = model.CurrentPhase,
			CurrentPhaseDescription = PhaseDescriptionResolver.TryResolve(model.GameType?.Code, model.CurrentPhase),
			CurrentHandNumber = model.CurrentHandNumber,
			DealerPosition = model.DealerPosition,
			Ante = model.Ante,
			SmallBlind = model.SmallBlind,
			BigBlind = model.BigBlind,
			BringIn = model.BringIn,
			SmallBet = model.SmallBet,
			BigBet = model.BigBet,
			MinBet = model.MinBet,
			GameSettings = model.GameSettings,
			Status = model.Status,
			CurrentPlayerIndex = model.CurrentPlayerIndex,
			BringInPlayerIndex = model.BringInPlayerIndex,
			CurrentDrawPlayerIndex = model.CurrentDrawPlayerIndex,
			RandomSeed = model.RandomSeed,
			CreatedAt = model.CreatedAt,
			UpdatedAt = model.UpdatedAt,
			StartedAt = model.StartedAt,
			EndedAt = model.EndedAt,
			CreatedById = model.CreatedById,
			CreatedByName = model.CreatedByName,
			CanContinue = activePlayers >= 2,
			RowVersion = MapRowVersion(model.RowVersion)
		};
	}

	public static IQueryable<GetGameResponse> ProjectToResponse(this IQueryable<Game> query, int minimumNumberOfPlayers, int maximumNumberOfPlayers)
	{
		return query.Select(model => ToResponse(model, minimumNumberOfPlayers, maximumNumberOfPlayers));
	}

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}
