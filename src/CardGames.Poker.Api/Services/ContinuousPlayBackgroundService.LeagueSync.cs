using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using CardGames.Contracts.SignalR;
using Microsoft.EntityFrameworkCore;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Services;

public sealed partial class ContinuousPlayBackgroundService
{
	private static async Task SyncLeagueCompletionIfNeededAsync(
		LeagueGameCompletionSyncService? leagueCompletionSync,
		Guid gameId,
		CancellationToken cancellationToken)
	{
		if (leagueCompletionSync is null)
		{
			return;
		}

		await leagueCompletionSync.SyncLeagueEventCompletionAsync(gameId, cancellationToken);
	}
}
