using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentBettingRound;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetCurrentBettingRoundMapper
{
	[MapProperty(nameof(BettingRound.Game) + "." + nameof(Game.Pots), nameof(GetCurrentBettingRoundResponse.TotalPot))]
	public static partial GetCurrentBettingRoundResponse ToResponse(this BettingRound model);

	[MapProperty(nameof(BettingRound.Game) + "." + nameof(Game.Pots), nameof(GetCurrentBettingRoundResponse.TotalPot))]
	public static partial IQueryable<GetCurrentBettingRoundResponse> ProjectToResponse(this IQueryable<BettingRound> query);

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();

	private static int MapTotalPot(ICollection<Pot> pots) => pots.Sum(p => p.Amount);
}
