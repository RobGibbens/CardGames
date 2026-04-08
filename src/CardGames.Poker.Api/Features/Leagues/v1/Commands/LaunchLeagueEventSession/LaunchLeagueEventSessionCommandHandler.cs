using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Leagues.v1;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using CardGames.Contracts.SignalR;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;

public sealed class LaunchLeagueEventSessionCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IMediator mediator,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<LaunchLeagueEventSessionCommand, OneOf<LaunchLeagueEventSessionResponse, LaunchLeagueEventSessionError>>
{
	public async Task<OneOf<LaunchLeagueEventSessionResponse, LaunchLeagueEventSessionError>> Handle(LaunchLeagueEventSessionCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.GameCode))
		{
			return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, "Game code is required.");
		}

		if (request.Request.HostStartingChips <= 0)
		{
			return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, "Host starting chips must be greater than 0.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.LeagueNotFound, "League not found.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.Forbidden, "Only league managers or admins can launch league event sessions.");
		}

		var now = DateTimeOffset.UtcNow;
		var gameId = Guid.CreateVersion7();
		var normalizedGameCode = string.Empty;
		var hostName = string.IsNullOrWhiteSpace(currentUserService.UserName)
			? currentUserService.UserId!
			: currentUserService.UserName;
		var gameName = string.IsNullOrWhiteSpace(request.Request.GameName)
			? $"League Event {request.EventId:N}" 
			: request.Request.GameName.Trim();

		if (request.SourceType == LeagueEventSourceType.Season)
		{
			if (!request.SeasonId.HasValue)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, "Season id is required for season events.");
			}

			var seasonEvent = await context.LeagueSeasonEvents
				.FirstOrDefaultAsync(x => x.Id == request.EventId, cancellationToken);

			if (seasonEvent is null)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.EventNotFound, "Season event not found.");
			}

			if (seasonEvent.LeagueId != request.LeagueId || seasonEvent.LeagueSeasonId != request.SeasonId.Value)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.MismatchedLeagueOrSeason, "Season event does not belong to the supplied league/season.");
			}

			if (seasonEvent.LaunchedGameId.HasValue)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.AlreadyLaunched, "This season event has already launched a table.");
			}

			if (!seasonEvent.TournamentBuyIn.HasValue)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, "Season events must define a tournament buy-in before launch.");
			}

			var gameCode = !string.IsNullOrWhiteSpace(seasonEvent.GameTypeCode)
				? seasonEvent.GameTypeCode
				: request.Request.GameCode;

			if (!LeagueEventGameSettings.TryResolve(gameCode, out normalizedGameCode, out var rules, out var gameTypeError))
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, gameTypeError!);
			}

			var ante = seasonEvent.Ante ?? request.Request.Ante;
			var minBet = seasonEvent.MinBet ?? request.Request.MinBet;
			var smallBlind = seasonEvent.SmallBlind ?? request.Request.SmallBlind;
			var bigBlind = seasonEvent.BigBlind ?? request.Request.BigBlind;

			var stakesError = LeagueEventGameSettings.Validate(rules!, ante, minBet, smallBlind, bigBlind);
			if (stakesError is not null)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, stakesError);
			}

			var createResult = await mediator.Send(
				new Features.Games.Common.v1.Commands.CreateGame.CreateGameCommand(
					gameId,
					normalizedGameCode,
					gameName,
					LeagueEventGameSettings.NormalizeAnte(rules!, ante),
					LeagueEventGameSettings.NormalizeMinBet(rules!, minBet, bigBlind),
					[new Features.Games.Common.v1.Commands.CreateGame.PlayerInfo(hostName, seasonEvent.TournamentBuyIn.Value)],
					SmallBlind: LeagueEventGameSettings.NormalizeSmallBlind(rules!, minBet, smallBlind, bigBlind),
					BigBlind: LeagueEventGameSettings.NormalizeBigBlind(rules!, minBet, bigBlind),
					TournamentBuyIn: seasonEvent.TournamentBuyIn),
				cancellationToken);

			if (createResult.IsT1)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.CreateGameConflict, createResult.AsT1.Reason);
			}

			seasonEvent.LaunchedGameId = gameId;
			seasonEvent.LaunchedByUserId = currentUserService.UserId;
			seasonEvent.LaunchedAtUtc = now;

			await context.SaveChangesAsync(cancellationToken);
			await leagueBroadcaster.BroadcastEventSessionLaunchedAsync(new LeagueEventSessionLaunchedDto
			{
				LeagueId = request.LeagueId,
				EventId = request.EventId,
				SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.Season,
				SeasonId = request.SeasonId,
				GameId = gameId,
				LaunchedAtUtc = now
			}, cancellationToken);
		}
		else
		{
			var oneOffEvent = await context.LeagueOneOffEvents
				.FirstOrDefaultAsync(x => x.Id == request.EventId, cancellationToken);

			if (oneOffEvent is null)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.EventNotFound, "One-off event not found.");
			}

			if (oneOffEvent.LeagueId != request.LeagueId)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.MismatchedLeagueOrSeason, "One-off event does not belong to the supplied league.");
			}

			if (oneOffEvent.LaunchedGameId.HasValue)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.AlreadyLaunched, "This one-off event has already launched a table.");
			}

			var hostStartingChips = request.Request.HostStartingChips;
			int? tournamentBuyIn = null;
			var gameCode = !string.IsNullOrWhiteSpace(oneOffEvent.GameTypeCode)
				? oneOffEvent.GameTypeCode
				: request.Request.GameCode;

			if (!LeagueEventGameSettings.TryResolve(gameCode, out normalizedGameCode, out var rules, out var gameTypeError))
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, gameTypeError!);
			}
			if (oneOffEvent.EventType == Data.Entities.LeagueOneOffEventType.Tournament)
			{
				if (!oneOffEvent.TournamentBuyIn.HasValue)
				{
					return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, "Tournament events must define a tournament buy-in before launch.");
				}

				hostStartingChips = oneOffEvent.TournamentBuyIn.Value;
				tournamentBuyIn = oneOffEvent.TournamentBuyIn.Value;
			}

			var ante = oneOffEvent.Ante;
			var minBet = oneOffEvent.MinBet;
			var smallBlind = oneOffEvent.SmallBlind ?? request.Request.SmallBlind;
			var bigBlind = oneOffEvent.BigBlind ?? request.Request.BigBlind;

			var stakesError = LeagueEventGameSettings.Validate(rules!, ante, minBet, smallBlind, bigBlind);
			if (stakesError is not null)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.InvalidRequest, stakesError);
			}

			var createResult = await mediator.Send(
				new Features.Games.Common.v1.Commands.CreateGame.CreateGameCommand(
					gameId,
					normalizedGameCode,
					gameName,
					LeagueEventGameSettings.NormalizeAnte(rules!, ante),
					LeagueEventGameSettings.NormalizeMinBet(rules!, minBet, bigBlind),
					[new Features.Games.Common.v1.Commands.CreateGame.PlayerInfo(hostName, hostStartingChips)],
					SmallBlind: LeagueEventGameSettings.NormalizeSmallBlind(rules!, minBet, smallBlind, bigBlind),
					BigBlind: LeagueEventGameSettings.NormalizeBigBlind(rules!, minBet, bigBlind),
					TournamentBuyIn: tournamentBuyIn),
				cancellationToken);

			if (createResult.IsT1)
			{
				return new LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode.CreateGameConflict, createResult.AsT1.Reason);
			}

			oneOffEvent.LaunchedGameId = gameId;
			oneOffEvent.LaunchedByUserId = currentUserService.UserId;
			oneOffEvent.LaunchedAtUtc = now;

			await context.SaveChangesAsync(cancellationToken);
			await leagueBroadcaster.BroadcastEventSessionLaunchedAsync(new LeagueEventSessionLaunchedDto
			{
				LeagueId = request.LeagueId,
				EventId = request.EventId,
				SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff,
				GameId = gameId,
				LaunchedAtUtc = now
			}, cancellationToken);
		}

		return new LaunchLeagueEventSessionResponse
		{
			LeagueId = request.LeagueId,
			EventId = request.EventId,
			GameId = gameId,
			GameCode = normalizedGameCode,
			TablePath = $"/table/{gameId}",
			LaunchedAtUtc = now
		};
	}
}
