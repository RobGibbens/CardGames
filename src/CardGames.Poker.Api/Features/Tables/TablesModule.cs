using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Tables;

public static class TablesModule
{
    public static IEndpointRouteBuilder MapTablesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tables")
            .WithTags("Tables");

        group.MapGet("", GetTablesAsync)
            .WithName("GetTables");

        return app;
    }

    private static async Task<IResult> GetTablesAsync(
        ITablesRepository tablesRepository,
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null,
        int? minAvailableSeats = null,
        bool? hideFullTables = null,
        bool? hideEmptyTables = null)
    {
        var tables = await tablesRepository.GetTablesAsync(
            variant,
            minSmallBlind,
            maxSmallBlind,
            minAvailableSeats,
            hideFullTables,
            hideEmptyTables);

        return Results.Ok(new TablesListResponse(true, Tables: tables));
    }
}
