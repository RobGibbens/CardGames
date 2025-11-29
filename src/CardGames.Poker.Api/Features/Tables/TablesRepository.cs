using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Tables;

public interface ITablesRepository
{
    Task<IReadOnlyList<TableSummaryDto>> GetTablesAsync(
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null,
        int? minAvailableSeats = null,
        bool? hideFullTables = null,
        bool? hideEmptyTables = null);

    Task<TableSummaryDto> CreateTableAsync(CreateTableRequest request);

    Task<TableSummaryDto?> GetTableByIdAsync(Guid tableId);

    Task<(bool Success, int? SeatNumber, string? Error)> JoinTableAsync(Guid tableId, string? password);

    Task<TableSummaryDto?> FindTableForQuickJoinAsync(
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null);
}

public class InMemoryTablesRepository : ITablesRepository
{
    private readonly List<TableSummaryDto> _tables;
    private readonly Dictionary<Guid, string?> _tablePasswords;

    public InMemoryTablesRepository()
    {
        _tablePasswords = new Dictionary<Guid, string?>();
        
        // Seed with sample tables for demonstration
        var passwordProtectedTableId = Guid.NewGuid();
        _tables =
        [
            new TableSummaryDto(
                Id: Guid.NewGuid(),
                Name: "Beginner's Table",
                Variant: PokerVariant.TexasHoldem,
                SmallBlind: 1,
                BigBlind: 2,
                MinBuyIn: 40,
                MaxBuyIn: 200,
                MaxSeats: 9,
                OccupiedSeats: 3,
                State: GameState.WaitingForPlayers,
                Privacy: TablePrivacy.Public,
                CreatedAt: DateTime.UtcNow.AddHours(-2)),

            new TableSummaryDto(
                Id: Guid.NewGuid(),
                Name: "Mid Stakes Holdem",
                Variant: PokerVariant.TexasHoldem,
                SmallBlind: 5,
                BigBlind: 10,
                MinBuyIn: 200,
                MaxBuyIn: 1000,
                MaxSeats: 6,
                OccupiedSeats: 5,
                State: GameState.BettingRound,
                Privacy: TablePrivacy.Public,
                CreatedAt: DateTime.UtcNow.AddHours(-1)),

            new TableSummaryDto(
                Id: Guid.NewGuid(),
                Name: "High Roller",
                Variant: PokerVariant.TexasHoldem,
                SmallBlind: 25,
                BigBlind: 50,
                MinBuyIn: 1000,
                MaxBuyIn: 5000,
                MaxSeats: 6,
                OccupiedSeats: 6,
                State: GameState.BettingRound,
                Privacy: TablePrivacy.Private,
                CreatedAt: DateTime.UtcNow.AddMinutes(-30)),

            new TableSummaryDto(
                Id: Guid.NewGuid(),
                Name: "Omaha Action",
                Variant: PokerVariant.Omaha,
                SmallBlind: 2,
                BigBlind: 5,
                MinBuyIn: 100,
                MaxBuyIn: 500,
                MaxSeats: 9,
                OccupiedSeats: 7,
                State: GameState.Dealing,
                Privacy: TablePrivacy.Public,
                CreatedAt: DateTime.UtcNow.AddMinutes(-45)),

            new TableSummaryDto(
                Id: Guid.NewGuid(),
                Name: "Seven Card Stud Classic",
                Variant: PokerVariant.SevenCardStud,
                SmallBlind: 1,
                BigBlind: 2,
                MinBuyIn: 40,
                MaxBuyIn: 200,
                MaxSeats: 8,
                OccupiedSeats: 0,
                State: GameState.WaitingForPlayers,
                Privacy: TablePrivacy.Public,
                CreatedAt: DateTime.UtcNow.AddHours(-3)),

            new TableSummaryDto(
                Id: passwordProtectedTableId,
                Name: "Five Card Draw Fun",
                Variant: PokerVariant.FiveCardDraw,
                SmallBlind: 1,
                BigBlind: 2,
                MinBuyIn: 40,
                MaxBuyIn: 200,
                MaxSeats: 6,
                OccupiedSeats: 2,
                State: GameState.WaitingForPlayers,
                Privacy: TablePrivacy.Password,
                CreatedAt: DateTime.UtcNow.AddMinutes(-15))
        ];
        
        // Set password for the password-protected table
        _tablePasswords[passwordProtectedTableId] = "demo123";
    }

    public Task<IReadOnlyList<TableSummaryDto>> GetTablesAsync(
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null,
        int? minAvailableSeats = null,
        bool? hideFullTables = null,
        bool? hideEmptyTables = null)
    {
        var query = _tables.AsEnumerable();

        if (variant.HasValue)
        {
            query = query.Where(t => t.Variant == variant.Value);
        }

        if (minSmallBlind.HasValue)
        {
            query = query.Where(t => t.SmallBlind >= minSmallBlind.Value);
        }

        if (maxSmallBlind.HasValue)
        {
            query = query.Where(t => t.SmallBlind <= maxSmallBlind.Value);
        }

        if (minAvailableSeats.HasValue)
        {
            query = query.Where(t => (t.MaxSeats - t.OccupiedSeats) >= minAvailableSeats.Value);
        }

        if (hideFullTables == true)
        {
            query = query.Where(t => t.OccupiedSeats < t.MaxSeats);
        }

        if (hideEmptyTables == true)
        {
            query = query.Where(t => t.OccupiedSeats > 0);
        }

        return Task.FromResult<IReadOnlyList<TableSummaryDto>>(query.ToList());
    }

    public Task<TableSummaryDto> CreateTableAsync(CreateTableRequest request)
    {
        var table = new TableSummaryDto(
            Id: Guid.NewGuid(),
            Name: request.Name,
            Variant: request.Variant,
            SmallBlind: request.SmallBlind,
            BigBlind: request.BigBlind,
            MinBuyIn: request.MinBuyIn,
            MaxBuyIn: request.MaxBuyIn,
            MaxSeats: request.MaxSeats,
            OccupiedSeats: 0,
            State: GameState.WaitingForPlayers,
            Privacy: request.Privacy,
            CreatedAt: DateTime.UtcNow);

        _tables.Add(table);
        
        if (request.Privacy == TablePrivacy.Password)
        {
            _tablePasswords[table.Id] = request.Password;
        }

        return Task.FromResult(table);
    }

    public Task<TableSummaryDto?> GetTableByIdAsync(Guid tableId)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        return Task.FromResult(table);
    }

    public Task<(bool Success, int? SeatNumber, string? Error)> JoinTableAsync(Guid tableId, string? password)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        
        if (table == null)
        {
            return Task.FromResult<(bool, int?, string?)>((false, null, "Table not found."));
        }

        if (table.OccupiedSeats >= table.MaxSeats)
        {
            return Task.FromResult<(bool, int?, string?)>((false, null, "Table is full."));
        }

        if (table.Privacy == TablePrivacy.Password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return Task.FromResult<(bool, int?, string?)>((false, null, "Password is required to join this table."));
            }

            if (!_tablePasswords.TryGetValue(tableId, out var storedPassword) || storedPassword != password)
            {
                return Task.FromResult<(bool, int?, string?)>((false, null, "Invalid password."));
            }
        }

        // Assign seat and update occupied seats
        var seatNumber = table.OccupiedSeats + 1;
        var index = _tables.FindIndex(t => t.Id == tableId);
        _tables[index] = table with { OccupiedSeats = table.OccupiedSeats + 1 };

        return Task.FromResult<(bool, int?, string?)>((true, seatNumber, null));
    }

    public Task<TableSummaryDto?> FindTableForQuickJoinAsync(
        PokerVariant? variant = null,
        int? minSmallBlind = null,
        int? maxSmallBlind = null)
    {
        var query = _tables
            .Where(t => t.Privacy == TablePrivacy.Public)
            .Where(t => t.OccupiedSeats < t.MaxSeats);

        if (variant.HasValue)
        {
            query = query.Where(t => t.Variant == variant.Value);
        }

        if (minSmallBlind.HasValue)
        {
            query = query.Where(t => t.SmallBlind >= minSmallBlind.Value);
        }

        if (maxSmallBlind.HasValue)
        {
            query = query.Where(t => t.SmallBlind <= maxSmallBlind.Value);
        }

        // Prefer tables with more players but still have seats available
        var table = query
            .OrderByDescending(t => t.OccupiedSeats)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefault();

        return Task.FromResult(table);
    }
}
