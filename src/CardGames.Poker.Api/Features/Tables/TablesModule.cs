using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;
using CardGames.Poker.Shared.Validation;
using Microsoft.AspNetCore.SignalR;

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

        group.MapPost("{tableId:guid}/waiting-list", JoinWaitingListAsync)
            .WithName("JoinWaitingList");

        group.MapDelete("{tableId:guid}/waiting-list/{playerName}", LeaveWaitingListAsync)
            .WithName("LeaveWaitingList");

        group.MapGet("{tableId:guid}/waiting-list", GetWaitingListAsync)
            .WithName("GetWaitingList");

        group.MapPost("{tableId:guid}/leave", LeaveTableAsync)
            .WithName("LeaveTable");

        // Seat management endpoints
        group.MapGet("{tableId:guid}/seats", GetSeatsAsync)
            .WithName("GetSeats");

        group.MapPost("{tableId:guid}/seats/select", SelectSeatAsync)
            .WithName("SelectSeat");

        group.MapPost("{tableId:guid}/seats/buy-in", BuyInAsync)
            .WithName("BuyIn");

        group.MapPost("{tableId:guid}/seats/sit-out", SitOutAsync)
            .WithName("SitOut");

        group.MapPost("{tableId:guid}/seats/change", SeatChangeAsync)
            .WithName("SeatChange");

        group.MapPost("{tableId:guid}/seats/stand-up", StandUpAsync)
            .WithName("StandUp");

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

        if (request.Privacy == TablePrivacy.Password && string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: "Password is required for password-protected tables."));
        }

        // Validate table configuration using TableConfigValidator
        var config = request.ToConfig();
        var configErrors = TableConfigValidator.Validate(config);
        if (configErrors.Count > 0)
        {
            return Results.BadRequest(new CreateTableResponse(false, Error: configErrors[0]));
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
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
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

        // Broadcast seat status change to lobby
        var updatedTable = await tablesRepository.GetTableByIdAsync(tableId);
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);
        }

        return Results.Ok(new JoinTableResponse(
            Success: true,
            TableId: tableId,
            SeatNumber: seatNumber));
    }

    private static async Task<IResult> QuickJoinAsync(
        QuickJoinRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
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

        // Broadcast seat status change to lobby
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                table.Id,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);
        }

        return Results.Ok(new QuickJoinResponse(
            Success: true,
            TableId: table.Id,
            SeatNumber: seatNumber,
            Table: updatedTable));
    }

    private static async Task<IResult> JoinWaitingListAsync(
        Guid tableId,
        JoinWaitingListRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new JoinWaitingListResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, entry, error) = await tablesRepository.JoinWaitingListAsync(tableId, request.PlayerName);

        if (!success)
        {
            return Results.BadRequest(new JoinWaitingListResponse(false, Error: error));
        }

        // Get updated table info to get accurate waiting list count
        var updatedTable = await tablesRepository.GetTableByIdAsync(tableId);
        
        // Broadcast waiting list update to table group
        var waitingListEvent = new PlayerJoinedWaitingListEvent(
            tableId,
            DateTime.UtcNow,
            request.PlayerName,
            entry!.Position,
            updatedTable?.WaitingListCount ?? entry.Position);

        await hubContext.Clients.Group(tableId.ToString()).SendAsync("PlayerJoinedWaitingList", waitingListEvent);

        // Broadcast seat status change to lobby
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);
        }

        return Results.Ok(new JoinWaitingListResponse(Success: true, Entry: entry));
    }

    private static async Task<IResult> LeaveWaitingListAsync(
        Guid tableId,
        string playerName,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        var (success, error) = await tablesRepository.LeaveWaitingListAsync(tableId, playerName);

        if (!success)
        {
            return Results.BadRequest(new LeaveWaitingListResponse(false, Error: error));
        }

        // Get updated waiting list count
        var waitingList = await tablesRepository.GetWaitingListAsync(tableId);

        // Broadcast waiting list update to table group
        var waitingListEvent = new PlayerLeftWaitingListEvent(
            tableId,
            DateTime.UtcNow,
            playerName,
            waitingList.Count);

        await hubContext.Clients.Group(tableId.ToString()).SendAsync("PlayerLeftWaitingList", waitingListEvent);

        // Broadcast seat status change to lobby
        var updatedTable = await tablesRepository.GetTableByIdAsync(tableId);
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);
        }

        return Results.Ok(new LeaveWaitingListResponse(Success: true));
    }

    private static async Task<IResult> GetWaitingListAsync(
        Guid tableId,
        ITablesRepository tablesRepository)
    {
        var table = await tablesRepository.GetTableByIdAsync(tableId);
        if (table == null)
        {
            return Results.NotFound(new GetWaitingListResponse(false, Error: "Table not found."));
        }

        var entries = await tablesRepository.GetWaitingListAsync(tableId);

        return Results.Ok(new GetWaitingListResponse(Success: true, Entries: entries));
    }

    private static async Task<IResult> LeaveTableAsync(
        Guid tableId,
        LeaveTableRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new LeaveTableResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, notifiedPlayer, error) = await tablesRepository.LeaveTableAsync(tableId, request.PlayerName);

        if (!success)
        {
            return Results.BadRequest(new LeaveTableResponse(false, Error: error));
        }

        var updatedTable = await tablesRepository.GetTableByIdAsync(tableId);

        // Broadcast seat availability to waiting players
        if (notifiedPlayer != null)
        {
            var seatAvailableEvent = new SeatAvailableEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable!.MaxSeats - updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.OccupiedSeats,
                updatedTable.WaitingListCount);

            // Notify the waiting list group for this table
            await hubContext.Clients.Group($"{tableId}-waitlist").SendAsync("SeatAvailable", seatAvailableEvent);
        }

        // Broadcast seat status change to lobby
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);
        }

        return Results.Ok(new LeaveTableResponse(Success: true));
    }

    #region Seat Management Endpoints

    private static async Task<IResult> GetSeatsAsync(
        Guid tableId,
        ITablesRepository tablesRepository)
    {
        var table = await tablesRepository.GetTableByIdAsync(tableId);
        if (table == null)
        {
            return Results.NotFound(new GetSeatsResponse(false, Error: "Table not found."));
        }

        var seats = await tablesRepository.GetSeatsAsync(tableId);

        return Results.Ok(new GetSeatsResponse(Success: true, TableId: tableId, Seats: seats));
    }

    private static async Task<IResult> SelectSeatAsync(
        Guid tableId,
        SelectSeatRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new SelectSeatResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, seat, reservedUntil, error) = await tablesRepository.SelectSeatAsync(
            tableId, request.SeatNumber, request.PlayerName);

        if (!success)
        {
            return Results.BadRequest(new SelectSeatResponse(false, Error: error));
        }

        // Broadcast seat reserved event to table
        var seatReservedEvent = new SeatReservedEvent(
            tableId,
            DateTime.UtcNow,
            request.SeatNumber,
            request.PlayerName,
            reservedUntil!.Value);

        await hubContext.Clients.Group(tableId.ToString()).SendAsync("SeatReserved", seatReservedEvent);

        return Results.Ok(new SelectSeatResponse(
            Success: true,
            TableId: tableId,
            SeatNumber: request.SeatNumber,
            ReservedUntil: reservedUntil,
            Seat: seat));
    }

    private static async Task<IResult> BuyInAsync(
        Guid tableId,
        BuyInRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new BuyInResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, seat, error) = await tablesRepository.BuyInAsync(
            tableId, request.SeatNumber, request.PlayerName, request.BuyInAmount);

        if (!success)
        {
            return Results.BadRequest(new BuyInResponse(false, Error: error));
        }

        // Broadcast seat taken event to table
        var seatTakenEvent = new SeatTakenEvent(
            tableId,
            DateTime.UtcNow,
            request.SeatNumber,
            request.PlayerName,
            request.BuyInAmount);

        await hubContext.Clients.Group(tableId.ToString()).SendAsync("SeatTaken", seatTakenEvent);

        // Broadcast player bought in event
        var playerBoughtInEvent = new PlayerBoughtInEvent(
            tableId,
            DateTime.UtcNow,
            request.SeatNumber,
            request.PlayerName,
            request.BuyInAmount);

        await hubContext.Clients.Group(tableId.ToString()).SendAsync("PlayerBoughtIn", playerBoughtInEvent);

        // Broadcast seat status change to lobby
        var updatedTable = await tablesRepository.GetTableByIdAsync(tableId);
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);
        }

        return Results.Ok(new BuyInResponse(
            Success: true,
            TableId: tableId,
            SeatNumber: request.SeatNumber,
            ChipStack: request.BuyInAmount,
            Seat: seat));
    }

    private static async Task<IResult> SitOutAsync(
        Guid tableId,
        SitOutRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new SitOutResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, isSittingOut, error) = await tablesRepository.SetSitOutStatusAsync(
            tableId, request.PlayerName, request.SitOut);

        if (!success)
        {
            return Results.BadRequest(new SitOutResponse(false, Error: error));
        }

        // Broadcast sit out / sit back in event to table
        var seats = await tablesRepository.GetSeatsAsync(tableId);
        var playerSeat = seats.FirstOrDefault(s => s.PlayerName == request.PlayerName);

        if (request.SitOut)
        {
            var playerSatOutEvent = new PlayerSatOutEvent(
                tableId,
                DateTime.UtcNow,
                playerSeat?.SeatNumber ?? 0,
                request.PlayerName);

            await hubContext.Clients.Group(tableId.ToString()).SendAsync("PlayerSatOut", playerSatOutEvent);
        }
        else
        {
            var playerSatBackInEvent = new PlayerSatBackInEvent(
                tableId,
                DateTime.UtcNow,
                playerSeat?.SeatNumber ?? 0,
                request.PlayerName);

            await hubContext.Clients.Group(tableId.ToString()).SendAsync("PlayerSatBackIn", playerSatBackInEvent);
        }

        return Results.Ok(new SitOutResponse(
            Success: true,
            TableId: tableId,
            PlayerName: request.PlayerName,
            IsSittingOut: isSittingOut));
    }

    private static async Task<IResult> SeatChangeAsync(
        Guid tableId,
        SeatChangeRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new SeatChangeResponse(false, Error: "Table ID in URL does not match request body."));
        }

        var (success, oldSeatNumber, newSeatNumber, seat, isPending, error) = await tablesRepository.RequestSeatChangeAsync(
            tableId, request.PlayerName, request.DesiredSeatNumber);

        if (!success)
        {
            return Results.BadRequest(new SeatChangeResponse(false, Error: error));
        }

        if (isPending)
        {
            // Broadcast seat change requested event
            var seatChangeRequestedEvent = new SeatChangeRequestedEvent(
                tableId,
                DateTime.UtcNow,
                request.PlayerName,
                oldSeatNumber!.Value,
                request.DesiredSeatNumber);

            await hubContext.Clients.Group(tableId.ToString()).SendAsync("SeatChangeRequested", seatChangeRequestedEvent);
        }
        else
        {
            // Broadcast seat change completed event
            var seatChangeCompletedEvent = new SeatChangeCompletedEvent(
                tableId,
                DateTime.UtcNow,
                request.PlayerName,
                oldSeatNumber!.Value,
                newSeatNumber!.Value);

            await hubContext.Clients.Group(tableId.ToString()).SendAsync("SeatChangeCompleted", seatChangeCompletedEvent);
        }

        return Results.Ok(new SeatChangeResponse(
            Success: true,
            TableId: tableId,
            OldSeatNumber: oldSeatNumber,
            NewSeatNumber: newSeatNumber,
            Seat: seat,
            IsPending: isPending));
    }

    private static async Task<IResult> StandUpAsync(
        Guid tableId,
        LeaveTableRequest request,
        ITablesRepository tablesRepository,
        IHubContext<GameHub> hubContext)
    {
        if (tableId != request.TableId)
        {
            return Results.BadRequest(new LeaveTableResponse(false, Error: "Table ID in URL does not match request body."));
        }

        // Get player's seat before standing up
        var seats = await tablesRepository.GetSeatsAsync(tableId);
        var playerSeat = seats.FirstOrDefault(s => s.PlayerName == request.PlayerName);

        var (success, cashoutAmount, error) = await tablesRepository.StandUpAsync(tableId, request.PlayerName);

        if (!success)
        {
            return Results.BadRequest(new LeaveTableResponse(false, Error: error));
        }

        // Broadcast seat freed event to table
        if (playerSeat != null)
        {
            var seatFreedEvent = new SeatFreedEvent(
                tableId,
                DateTime.UtcNow,
                playerSeat.SeatNumber,
                request.PlayerName,
                cashoutAmount);

            await hubContext.Clients.Group(tableId.ToString()).SendAsync("SeatFreed", seatFreedEvent);
        }

        // Broadcast seat status change to lobby
        var updatedTable = await tablesRepository.GetTableByIdAsync(tableId);
        if (updatedTable != null)
        {
            var seatStatusEvent = new TableSeatStatusChangedEvent(
                tableId,
                DateTime.UtcNow,
                updatedTable.OccupiedSeats,
                updatedTable.MaxSeats,
                updatedTable.WaitingListCount);

            await hubContext.Clients.Group("lobby").SendAsync("TableSeatStatusChanged", seatStatusEvent);

            // Notify waiting players if there's a seat available
            var waitingList = await tablesRepository.GetWaitingListAsync(tableId);
            if (waitingList.Count > 0)
            {
                var seatAvailableEvent = new SeatAvailableEvent(
                    tableId,
                    DateTime.UtcNow,
                    updatedTable.MaxSeats - updatedTable.OccupiedSeats,
                    updatedTable.MaxSeats,
                    updatedTable.OccupiedSeats,
                    waitingList.Count);

                await hubContext.Clients.Group($"{tableId}-waitlist").SendAsync("SeatAvailable", seatAvailableEvent);
            }
        }

        return Results.Ok(new LeaveTableResponse(Success: true));
    }

    #endregion
}
