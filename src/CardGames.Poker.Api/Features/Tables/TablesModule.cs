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

        group.MapPost("", CreateTableAsync)
            .WithName("CreateTable");

        group.MapPost("{tableId:guid}/join", JoinTableAsync)
            .WithName("JoinTable");

        group.MapPost("quick-join", QuickJoinAsync)
            .WithName("QuickJoin");

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

    private static async Task<IResult> CreateTableAsync(
        CreateTableRequest request,
        ITablesRepository tablesRepository,
        HttpContext httpContext)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Table name is required."));
        }

        if (request.Name.Length > 50)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Table name must be 50 characters or less."));
        }

        if (request.SmallBlind <= 0)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Small blind must be greater than 0."));
        }

        if (request.BigBlind <= 0)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Big blind must be greater than 0."));
        }

        if (request.BigBlind < request.SmallBlind)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Big blind must be greater than or equal to small blind."));
        }

        if (request.MinBuyIn <= 0)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Minimum buy-in must be greater than 0."));
        }

        if (request.MaxBuyIn < request.MinBuyIn)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Maximum buy-in must be greater than or equal to minimum buy-in."));
        }

        if (request.MaxSeats < 2 || request.MaxSeats > 10)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Number of seats must be between 2 and 10."));
        }

        if (request.Privacy == TablePrivacy.Password && string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Password is required for password-protected tables."));
        }

        var table = await tablesRepository.CreateTableAsync(request);

        // Generate the join link using the request's scheme and host
        var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
        var joinLink = $"{baseUrl}/tables/{table.Id}/join";

        return Results.Created(
            $"/api/tables/{table.Id}",
            new CreateTableResponse(
                Success: true,
                TableId: table.Id,
                JoinLink: joinLink,
                Table: table));
    }

    private static async Task<IResult> JoinTableAsync(
        Guid tableId,
        JoinTableRequest request,
        ITablesRepository tablesRepository)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new JoinTableResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, seatNumber, error) = await tablesRepository.JoinTableAsync(tableId, request.Password);

        if (!success)
        {
            return Results.BadRequest(new JoinTableResponse(false, Error: error));
        }

        return Results.Ok(new JoinTableResponse(
            Success: true,
            TableId: tableId,
            SeatNumber: seatNumber));
    }

    private static async Task<IResult> QuickJoinAsync(
        QuickJoinRequest request,
        ITablesRepository tablesRepository)
    {
        var table = await tablesRepository.FindTableForQuickJoinAsync(
            request.Variant,
            request.MinSmallBlind,
            request.MaxSmallBlind);

        if (table == null)
        {
            return Results.Ok(new QuickJoinResponse(false, Error: "No suitable table found. Try creating a new table or adjusting your filters."));
        }

        var (success, seatNumber, error) = await tablesRepository.JoinTableAsync(table.Id, null);

        if (!success)
        {
            return Results.BadRequest(new QuickJoinResponse(false, Error: error));
        }

        // Get updated table info after joining
        var updatedTable = await tablesRepository.GetTableByIdAsync(table.Id);

        return Results.Ok(new QuickJoinResponse(
            Success: true,
            TableId: table.Id,
            SeatNumber: seatNumber,
            Table: updatedTable));
    }
}
