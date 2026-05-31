using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGameMapper
{
	// This mapper is pure projection. Business-derived values such as CanContinue are decided by an
	// authoritative layer (see GameContinuationPolicy) and passed in already computed.
	public static GetGameResponse ToResponse(this Game model, int minimumNumberOfPlayers, int maximumNumberOfPlayers, bool canContinue)
	{
		return new GetGameResponse
		{
			Id = model.Id,
			GameTypeId = model.GameTypeId ?? Guid.Empty,
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
			MaxBuyIn = model.MaxBuyIn,
			TournamentBuyIn = model.TournamentBuyIn,
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
			CanContinue = canContinue,
			RowVersion = MapRowVersion(model.RowVersion),
			IsDealersChoice = model.IsDealersChoice,
			DealersChoiceDealerPosition = model.DealersChoiceDealerPosition,
			AreOddsVisibleToAllPlayers = model.AreOddsVisibleToAllPlayers,
			RequiresJoinApproval = model.RequiresJoinApproval
		};
	}

	public static IQueryable<GetGameResponse> ProjectToResponse(this IQueryable<Game> query, int minimumNumberOfPlayers, int maximumNumberOfPlayers, bool canContinue)
	{
		return query.Select(model => ToResponse(model, minimumNumberOfPlayers, maximumNumberOfPlayers, canContinue));
	}

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}
