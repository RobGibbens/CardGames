using CardGames.Poker.Shared.Contracts.History;

namespace CardGames.Poker.Api.Features.History;

public static class HistoryModule
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/history")
            .WithTags("Game History");

        group.MapGet("/", GetHistoryAsync)
            .WithName("GetHistory")
            .RequireAuthorization();

        group.MapGet("/{id}", GetGameByIdAsync)
            .WithName("GetGameById")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetHistoryAsync(
        HttpContext context,
        IHistoryRepository historyRepository,
        int page = 1,
        int pageSize = 20)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var history = await historyRepository.GetPlayerHistoryAsync(userId, page, pageSize);
        var totalCount = await historyRepository.GetPlayerGameCountAsync(userId);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = history.Select(h => new GameHistoryDto(
            h.Id,
            h.GameType,
            h.StartedAt,
            h.EndedAt,
            h.Won,
            h.ChipsWon,
            h.ChipsLost,
            h.PlayerCount,
            h.TableName,
            h.TournamentName,
            h.HandSummary
        )).ToList();

        return Results.Ok(new GameHistoryResponse(
            true,
            items,
            totalCount,
            page,
            pageSize,
            totalPages
        ));
    }

    private static async Task<IResult> GetGameByIdAsync(
        string id,
        HttpContext context,
        IHistoryRepository historyRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var record = await historyRepository.GetByIdAsync(id);
        if (record is null || record.UserId != userId)
        {
            return Results.NotFound(new GameHistoryResponse(false, Error: "Game not found"));
        }

        var dto = new GameHistoryDto(
            record.Id,
            record.GameType,
            record.StartedAt,
            record.EndedAt,
            record.Won,
            record.ChipsWon,
            record.ChipsLost,
            record.PlayerCount,
            record.TableName,
            record.TournamentName,
            record.HandSummary
        );

        return Results.Ok(new GameHistoryResponse(true, [dto]));
    }

    private static string? GetUserId(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
    }
}
