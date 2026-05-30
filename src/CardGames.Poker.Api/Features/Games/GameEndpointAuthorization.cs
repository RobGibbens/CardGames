using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games;

public enum GameActorScope
{
    CurrentPlayer,
    CurrentDrawPlayer
}

public interface IGameAuthorizationService
{
    bool IsAuthenticated { get; }

    Task<bool> IsHostAsync(Guid gameId, CancellationToken cancellationToken);

    Task<bool> IsParticipantAsync(Guid gameId, CancellationToken cancellationToken);

    Task<bool> IsCurrentActorAsync(Guid gameId, GameActorScope scope, CancellationToken cancellationToken);

    Task<bool> OwnsPlayerAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken);
}

internal sealed record CurrentGameCaller(Guid PlayerId, int SeatPosition);

public sealed class GameAuthorizationService(
    CardsDbContext context,
    ICurrentUserService currentUserService)
    : IGameAuthorizationService
{
    public bool IsAuthenticated => currentUserService.IsAuthenticated;

    public async Task<bool> IsHostAsync(Guid gameId, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        var currentUserId = currentUserService.UserId;
        var currentUserName = currentUserService.UserName;
        var currentUserEmail = currentUserService.UserEmail;

        var host = await context.Games
            .AsNoTracking()
            .Where(game => game.Id == gameId)
            .Select(game => new { game.CreatedById, game.CreatedByName })
            .FirstOrDefaultAsync(cancellationToken);

        if (host is null)
        {
            return false;
        }

        return MatchesHost(host.CreatedById, host.CreatedByName, currentUserId, currentUserName, currentUserEmail);
    }

    public async Task<bool> IsParticipantAsync(Guid gameId, CancellationToken cancellationToken)
    {
        return await GetCurrentCallerAsync(gameId, cancellationToken) is not null;
    }

    public async Task<bool> IsCurrentActorAsync(Guid gameId, GameActorScope scope, CancellationToken cancellationToken)
    {
        var caller = await GetCurrentCallerAsync(gameId, cancellationToken);
        if (caller is null)
        {
            return false;
        }

        var actorSeat = await context.Games
            .AsNoTracking()
            .Where(game => game.Id == gameId)
            .Select(game => scope == GameActorScope.CurrentDrawPlayer
                ? game.CurrentDrawPlayerIndex
                : game.CurrentPlayerIndex)
            .FirstOrDefaultAsync(cancellationToken);

        return actorSeat >= 0 && caller.SeatPosition == actorSeat;
    }

    public async Task<bool> OwnsPlayerAsync(Guid gameId, Guid playerId, CancellationToken cancellationToken)
    {
        var caller = await GetCurrentCallerAsync(gameId, cancellationToken);
        return caller is not null && caller.PlayerId == playerId;
    }

    private async Task<CurrentGameCaller?> GetCurrentCallerAsync(Guid gameId, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            return null;
        }

        var currentUserId = currentUserService.UserId;
        var currentUserName = currentUserService.UserName;
        var currentUserEmail = currentUserService.UserEmail;

        if (string.IsNullOrWhiteSpace(currentUserId)
            && string.IsNullOrWhiteSpace(currentUserName)
            && string.IsNullOrWhiteSpace(currentUserEmail))
        {
            return null;
        }

        return await context.GamePlayers
            .AsNoTracking()
            .Where(gamePlayer => gamePlayer.GameId == gameId && gamePlayer.Status != GamePlayerStatus.Left)
            .Where(gamePlayer =>
                (!string.IsNullOrWhiteSpace(currentUserId) && gamePlayer.Player.ExternalId == currentUserId)
                || (!string.IsNullOrWhiteSpace(currentUserEmail) && gamePlayer.Player.Email == currentUserEmail)
                || (!string.IsNullOrWhiteSpace(currentUserName) && gamePlayer.Player.Name == currentUserName))
            .Select(gamePlayer => new CurrentGameCaller(gamePlayer.PlayerId, gamePlayer.SeatPosition))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool MatchesHost(
        string? createdById,
        string? createdByName,
        string? currentUserId,
        string? currentUserName,
        string? currentUserEmail)
    {
        if (!string.IsNullOrWhiteSpace(createdById)
            && !string.IsNullOrWhiteSpace(currentUserId)
            && string.Equals(createdById, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(createdByName))
        {
            return false;
        }

        return (!string.IsNullOrWhiteSpace(currentUserName)
                && string.Equals(createdByName, currentUserName, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(currentUserEmail)
                && string.Equals(createdByName, currentUserEmail, StringComparison.OrdinalIgnoreCase));
    }
}

public static class GameEndpointAuthorizationExtensions
{
    public static RouteGroupBuilder RequireGameHostAuthorization(this RouteGroupBuilder group)
    {
        return AddAuthorizationFilter(group, static (service, _, gameId, cancellationToken) =>
            service.IsHostAsync(gameId, cancellationToken));
    }

    public static RouteGroupBuilder RequireGameParticipantAuthorization(this RouteGroupBuilder group)
    {
        return AddAuthorizationFilter(group, static (service, _, gameId, cancellationToken) =>
            service.IsParticipantAsync(gameId, cancellationToken));
    }

    public static RouteGroupBuilder RequireGameCurrentPlayerAuthorization(this RouteGroupBuilder group)
    {
        return AddAuthorizationFilter(group, static (service, _, gameId, cancellationToken) =>
            service.IsCurrentActorAsync(gameId, GameActorScope.CurrentPlayer, cancellationToken));
    }

    public static RouteGroupBuilder RequireGameCurrentDrawPlayerAuthorization(this RouteGroupBuilder group)
    {
        return AddAuthorizationFilter(group, static (service, _, gameId, cancellationToken) =>
            service.IsCurrentActorAsync(gameId, GameActorScope.CurrentDrawPlayer, cancellationToken));
    }

    public static RouteGroupBuilder RequireCallerOwnsTargetPlayerAuthorization(this RouteGroupBuilder group)
    {
        return AddAuthorizationFilter(group, static (service, invocationContext, gameId, cancellationToken) =>
        {
            if (!TryGetPlayerId(invocationContext, out var playerId))
            {
                return Task.FromResult(false);
            }

            return service.OwnsPlayerAsync(gameId, playerId, cancellationToken);
        });
    }

    private static RouteGroupBuilder AddAuthorizationFilter(
        RouteGroupBuilder group,
        Func<IGameAuthorizationService, EndpointFilterInvocationContext, Guid, CancellationToken, Task<bool>> authorize)
    {
        group.AddEndpointFilterFactory((_, next) => async invocationContext =>
        {
            var authorizationService = invocationContext.HttpContext.RequestServices.GetRequiredService<IGameAuthorizationService>();
            if (!authorizationService.IsAuthenticated)
            {
                return TypedResults.Unauthorized();
            }

            if (!TryGetGameId(invocationContext.HttpContext, out var gameId))
            {
                return Results.Problem(
                    title: "Game authorization misconfigured",
                    detail: "Expected a gameId route value for this endpoint.",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            var isAuthorized = await authorize(
                authorizationService,
                invocationContext,
                gameId,
                invocationContext.HttpContext.RequestAborted);

            return isAuthorized
                ? await next(invocationContext)
                : TypedResults.Forbid();
        });

        return group;
    }

    private static bool TryGetGameId(HttpContext httpContext, out Guid gameId)
    {
        gameId = Guid.Empty;
        var routeValue = httpContext.Request.RouteValues.TryGetValue("gameId", out var rawValue)
            ? rawValue?.ToString()
            : null;

        return Guid.TryParse(routeValue, out gameId);
    }

    private static bool TryGetPlayerId(EndpointFilterInvocationContext invocationContext, out Guid playerId)
    {
        playerId = Guid.Empty;

        if (invocationContext.HttpContext.Request.RouteValues.TryGetValue("playerId", out var rawRouteValue)
            && Guid.TryParse(rawRouteValue?.ToString(), out playerId))
        {
            return true;
        }

        foreach (var argument in invocationContext.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var playerIdProperty = argument.GetType().GetProperty("PlayerId");
            if (playerIdProperty is null || playerIdProperty.PropertyType != typeof(Guid))
            {
                continue;
            }

            var value = playerIdProperty.GetValue(argument);
            if (value is Guid guid && guid != Guid.Empty)
            {
                playerId = guid;
                return true;
            }
        }

        return false;
    }
}