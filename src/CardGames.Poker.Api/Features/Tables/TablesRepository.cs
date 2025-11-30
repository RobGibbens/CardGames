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

    Task<(bool Success, WaitingListEntryDto? Entry, string? Error)> JoinWaitingListAsync(Guid tableId, string playerName);

    Task<(bool Success, string? Error)> LeaveWaitingListAsync(Guid tableId, string playerName);

    Task<IReadOnlyList<WaitingListEntryDto>> GetWaitingListAsync(Guid tableId);

    Task<(bool Success, string? NotifiedPlayer, string? Error)> LeaveTableAsync(Guid tableId, string playerName);

    Task<WaitingListEntryDto?> GetNextWaitingPlayerAsync(Guid tableId);

    // Seat management methods
    Task<IReadOnlyList<SeatDto>> GetSeatsAsync(Guid tableId);

    Task<(bool Success, SeatDto? Seat, DateTime? ReservedUntil, string? Error)> SelectSeatAsync(
        Guid tableId, int seatNumber, string playerName);

    Task<(bool Success, SeatDto? Seat, string? Error)> BuyInAsync(
        Guid tableId, int seatNumber, string playerName, int buyInAmount);

    Task<(bool Success, bool? IsSittingOut, string? Error)> SetSitOutStatusAsync(
        Guid tableId, string playerName, bool sitOut);

    Task<(bool Success, int? OldSeatNumber, int? NewSeatNumber, SeatDto? Seat, bool IsPending, string? Error)> RequestSeatChangeAsync(
        Guid tableId, string playerName, int desiredSeatNumber);

    Task<(bool Success, int? CashoutAmount, string? Error)> StandUpAsync(
        Guid tableId, string playerName);

    Task CleanupExpiredReservationsAsync();
}

/// <summary>
/// Represents seat change request pending execution between hands.
/// </summary>
public record PendingSeatChangeRequest(
    Guid TableId,
    string PlayerName,
    int CurrentSeat,
    int DesiredSeat,
    DateTime RequestedAt);

public class InMemoryTablesRepository : ITablesRepository
{
    private readonly List<TableSummaryDto> _tables;
    private readonly Dictionary<Guid, string?> _tablePasswords;
    private readonly Dictionary<Guid, List<WaitingListEntryDto>> _waitingLists;
    private readonly Dictionary<Guid, List<SeatDto>> _tableSeats;
    private readonly List<PendingSeatChangeRequest> _pendingSeatChanges;
    private readonly TimeSpan _seatReservationTimeout = TimeSpan.FromMinutes(2);

    public InMemoryTablesRepository()
    {
        _tablePasswords = new Dictionary<Guid, string?>();
        _waitingLists = new Dictionary<Guid, List<WaitingListEntryDto>>();
        _tableSeats = new Dictionary<Guid, List<SeatDto>>();
        _pendingSeatChanges = new List<PendingSeatChangeRequest>();
        
        // Seed with sample tables for demonstration
        var passwordProtectedTableId = Guid.NewGuid();
        var fullTableId = Guid.NewGuid();
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
                Id: fullTableId,
                Name: "High Roller",
                Variant: PokerVariant.TexasHoldem,
                SmallBlind: 25,
                BigBlind: 50,
                MinBuyIn: 1000,
                MaxBuyIn: 5000,
                MaxSeats: 6,
                OccupiedSeats: 6,
                State: GameState.BettingRound,
                Privacy: TablePrivacy.Public,
                CreatedAt: DateTime.UtcNow.AddMinutes(-30),
                WaitingListCount: 2),

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

        // Seed waiting list for the full table
        _waitingLists[fullTableId] =
        [
            new WaitingListEntryDto(fullTableId, "WaitingPlayer1", DateTime.UtcNow.AddMinutes(-10), 1),
            new WaitingListEntryDto(fullTableId, "WaitingPlayer2", DateTime.UtcNow.AddMinutes(-5), 2)
        ];

        // Initialize seats for each table
        foreach (var table in _tables)
        {
            InitializeSeatsForTable(table);
        }
    }

    private void InitializeSeatsForTable(TableSummaryDto table)
    {
        var seats = new List<SeatDto>();
        for (int i = 1; i <= table.MaxSeats; i++)
        {
            // For occupied seats, create sample players
            if (i <= table.OccupiedSeats)
            {
                seats.Add(new SeatDto(
                    SeatNumber: i,
                    Status: SeatStatus.Occupied,
                    PlayerName: $"Player{i}",
                    ChipStack: table.MinBuyIn * 2));
            }
            else
            {
                seats.Add(new SeatDto(
                    SeatNumber: i,
                    Status: SeatStatus.Available));
            }
        }
        _tableSeats[table.Id] = seats;
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
            CreatedAt: DateTime.UtcNow,
            LimitType: request.LimitType,
            Ante: request.Ante);

        _tables.Add(table);
        
        if (request.Privacy == TablePrivacy.Password)
        {
            _tablePasswords[table.Id] = request.Password;
        }

        // Initialize seats for the new table
        InitializeSeatsForTable(table);

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

    public Task<(bool Success, WaitingListEntryDto? Entry, string? Error)> JoinWaitingListAsync(Guid tableId, string playerName)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        
        if (table == null)
        {
            return Task.FromResult<(bool, WaitingListEntryDto?, string?)>((false, null, "Table not found."));
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Task.FromResult<(bool, WaitingListEntryDto?, string?)>((false, null, "Player name is required."));
        }

        // Check if player is already on the waiting list
        if (!_waitingLists.TryGetValue(tableId, out var waitingList))
        {
            waitingList = [];
            _waitingLists[tableId] = waitingList;
        }

        if (waitingList.Any(e => e.PlayerName == playerName))
        {
            return Task.FromResult<(bool, WaitingListEntryDto?, string?)>((false, null, "Player is already on the waiting list."));
        }

        // Add to waiting list
        var position = waitingList.Count + 1;
        var entry = new WaitingListEntryDto(tableId, playerName, DateTime.UtcNow, position);
        waitingList.Add(entry);

        // Update table's waiting list count
        var index = _tables.FindIndex(t => t.Id == tableId);
        _tables[index] = table with { WaitingListCount = waitingList.Count };

        return Task.FromResult<(bool, WaitingListEntryDto?, string?)>((true, entry, null));
    }

    public Task<(bool Success, string? Error)> LeaveWaitingListAsync(Guid tableId, string playerName)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        
        if (table == null)
        {
            return Task.FromResult<(bool, string?)>((false, "Table not found."));
        }

        if (!_waitingLists.TryGetValue(tableId, out var waitingList))
        {
            return Task.FromResult<(bool, string?)>((false, "Player is not on the waiting list."));
        }

        var entry = waitingList.FirstOrDefault(e => e.PlayerName == playerName);
        if (entry == null)
        {
            return Task.FromResult<(bool, string?)>((false, "Player is not on the waiting list."));
        }

        // Remove from waiting list
        waitingList.Remove(entry);

        // Update positions for remaining entries
        for (int i = 0; i < waitingList.Count; i++)
        {
            var currentEntry = waitingList[i];
            if (currentEntry.Position != i + 1)
            {
                waitingList[i] = currentEntry with { Position = i + 1 };
            }
        }

        // Update table's waiting list count
        var index = _tables.FindIndex(t => t.Id == tableId);
        _tables[index] = table with { WaitingListCount = waitingList.Count };

        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<IReadOnlyList<WaitingListEntryDto>> GetWaitingListAsync(Guid tableId)
    {
        if (_waitingLists.TryGetValue(tableId, out var waitingList))
        {
            return Task.FromResult<IReadOnlyList<WaitingListEntryDto>>(waitingList.OrderBy(e => e.Position).ToList());
        }

        return Task.FromResult<IReadOnlyList<WaitingListEntryDto>>([]);
    }

    public Task<(bool Success, string? NotifiedPlayer, string? Error)> LeaveTableAsync(Guid tableId, string playerName)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        
        if (table == null)
        {
            return Task.FromResult<(bool, string?, string?)>((false, null, "Table not found."));
        }

        if (table.OccupiedSeats <= 0)
        {
            return Task.FromResult<(bool, string?, string?)>((false, null, "Table has no players."));
        }

        // Decrease occupied seats
        var index = _tables.FindIndex(t => t.Id == tableId);
        _tables[index] = table with { OccupiedSeats = table.OccupiedSeats - 1 };

        // Check if there's someone on the waiting list to notify
        string? notifiedPlayer = null;
        if (_waitingLists.TryGetValue(tableId, out var waitingList) && waitingList.Count > 0)
        {
            notifiedPlayer = waitingList[0].PlayerName;
        }

        return Task.FromResult<(bool, string?, string?)>((true, notifiedPlayer, null));
    }

    public Task<WaitingListEntryDto?> GetNextWaitingPlayerAsync(Guid tableId)
    {
        if (_waitingLists.TryGetValue(tableId, out var waitingList) && waitingList.Count > 0)
        {
            return Task.FromResult<WaitingListEntryDto?>(waitingList.OrderBy(e => e.Position).First());
        }

        return Task.FromResult<WaitingListEntryDto?>(null);
    }

    #region Seat Management

    public Task<IReadOnlyList<SeatDto>> GetSeatsAsync(Guid tableId)
    {
        if (_tableSeats.TryGetValue(tableId, out var seats))
        {
            // Clean up expired reservations first
            var now = DateTime.UtcNow;
            for (int i = 0; i < seats.Count; i++)
            {
                var seat = seats[i];
                if (seat.Status == SeatStatus.Reserved && seat.ReservedUntil.HasValue && seat.ReservedUntil.Value < now)
                {
                    seats[i] = seat with { Status = SeatStatus.Available, PlayerName = null, ReservedUntil = null };
                }
            }
            return Task.FromResult<IReadOnlyList<SeatDto>>(seats.OrderBy(s => s.SeatNumber).ToList());
        }

        return Task.FromResult<IReadOnlyList<SeatDto>>([]);
    }

    public Task<(bool Success, SeatDto? Seat, DateTime? ReservedUntil, string? Error)> SelectSeatAsync(
        Guid tableId, int seatNumber, string playerName)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        if (table == null)
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, "Table not found."));
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, "Player name is required."));
        }

        if (seatNumber < 1 || seatNumber > table.MaxSeats)
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, $"Seat number must be between 1 and {table.MaxSeats}."));
        }

        if (!_tableSeats.TryGetValue(tableId, out var seats))
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, "Table seats not initialized."));
        }

        // Check if player is already seated at this table
        var existingSeat = seats.FirstOrDefault(s => s.PlayerName == playerName && (s.Status == SeatStatus.Occupied || s.Status == SeatStatus.Reserved || s.Status == SeatStatus.SittingOut));
        if (existingSeat != null)
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, $"Player '{playerName}' is already seated at seat {existingSeat.SeatNumber}."));
        }

        var seatIndex = seats.FindIndex(s => s.SeatNumber == seatNumber);
        if (seatIndex == -1)
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, "Seat not found."));
        }

        var seat = seats[seatIndex];
        
        // Clean up expired reservation if any
        var now = DateTime.UtcNow;
        if (seat.Status == SeatStatus.Reserved && seat.ReservedUntil.HasValue && seat.ReservedUntil.Value < now)
        {
            seat = seat with { Status = SeatStatus.Available, PlayerName = null, ReservedUntil = null };
            seats[seatIndex] = seat;
        }

        if (seat.Status != SeatStatus.Available)
        {
            return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((false, null, null, $"Seat {seatNumber} is not available. Current status: {seat.Status}."));
        }

        // Reserve the seat
        var reservedUntil = now.Add(_seatReservationTimeout);
        var updatedSeat = new SeatDto(
            SeatNumber: seatNumber,
            Status: SeatStatus.Reserved,
            PlayerName: playerName,
            ReservedUntil: reservedUntil);

        seats[seatIndex] = updatedSeat;

        return Task.FromResult<(bool, SeatDto?, DateTime?, string?)>((true, updatedSeat, reservedUntil, null));
    }

    public Task<(bool Success, SeatDto? Seat, string? Error)> BuyInAsync(
        Guid tableId, int seatNumber, string playerName, int buyInAmount)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        if (table == null)
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, "Table not found."));
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, "Player name is required."));
        }

        if (buyInAmount < table.MinBuyIn)
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, $"Buy-in amount must be at least {table.MinBuyIn}."));
        }

        if (buyInAmount > table.MaxBuyIn)
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, $"Buy-in amount cannot exceed {table.MaxBuyIn}."));
        }

        if (!_tableSeats.TryGetValue(tableId, out var seats))
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, "Table seats not initialized."));
        }

        var seatIndex = seats.FindIndex(s => s.SeatNumber == seatNumber);
        if (seatIndex == -1)
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, "Seat not found."));
        }

        var seat = seats[seatIndex];

        // Check if the seat is reserved for this player
        if (seat.Status == SeatStatus.Reserved)
        {
            if (seat.PlayerName != playerName)
            {
                return Task.FromResult<(bool, SeatDto?, string?)>((false, null, $"Seat {seatNumber} is reserved for another player."));
            }

            // Check if reservation has expired
            if (seat.ReservedUntil.HasValue && seat.ReservedUntil.Value < DateTime.UtcNow)
            {
                // Expire the reservation
                seats[seatIndex] = seat with { Status = SeatStatus.Available, PlayerName = null, ReservedUntil = null };
                return Task.FromResult<(bool, SeatDto?, string?)>((false, null, "Seat reservation has expired. Please select the seat again."));
            }
        }
        else if (seat.Status == SeatStatus.Available)
        {
            // Allow direct buy-in without prior seat selection for simpler flow
            if (!string.IsNullOrEmpty(seat.PlayerName) && seat.PlayerName != playerName)
            {
                return Task.FromResult<(bool, SeatDto?, string?)>((false, null, $"Seat {seatNumber} is not available."));
            }
        }
        else
        {
            return Task.FromResult<(bool, SeatDto?, string?)>((false, null, $"Seat {seatNumber} is not available for buy-in. Current status: {seat.Status}."));
        }

        // Complete the buy-in
        var updatedSeat = new SeatDto(
            SeatNumber: seatNumber,
            Status: SeatStatus.Occupied,
            PlayerName: playerName,
            ChipStack: buyInAmount,
            ReservedUntil: null);

        seats[seatIndex] = updatedSeat;

        // Update table's occupied seats count
        var tableIndex = _tables.FindIndex(t => t.Id == tableId);
        var currentOccupied = seats.Count(s => s.Status == SeatStatus.Occupied);
        _tables[tableIndex] = table with { OccupiedSeats = currentOccupied };

        return Task.FromResult<(bool, SeatDto?, string?)>((true, updatedSeat, null));
    }

    public Task<(bool Success, bool? IsSittingOut, string? Error)> SetSitOutStatusAsync(
        Guid tableId, string playerName, bool sitOut)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        if (table == null)
        {
            return Task.FromResult<(bool, bool?, string?)>((false, null, "Table not found."));
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Task.FromResult<(bool, bool?, string?)>((false, null, "Player name is required."));
        }

        if (!_tableSeats.TryGetValue(tableId, out var seats))
        {
            return Task.FromResult<(bool, bool?, string?)>((false, null, "Table seats not initialized."));
        }

        var seatIndex = seats.FindIndex(s => s.PlayerName == playerName && (s.Status == SeatStatus.Occupied || s.Status == SeatStatus.SittingOut));
        if (seatIndex == -1)
        {
            return Task.FromResult<(bool, bool?, string?)>((false, null, $"Player '{playerName}' is not seated at this table."));
        }

        var seat = seats[seatIndex];

        if (sitOut && seat.Status == SeatStatus.SittingOut)
        {
            return Task.FromResult<(bool, bool?, string?)>((false, null, "Player is already sitting out."));
        }

        if (!sitOut && seat.Status == SeatStatus.Occupied)
        {
            return Task.FromResult<(bool, bool?, string?)>((false, null, "Player is already active."));
        }

        var updatedSeat = seat with 
        { 
            Status = sitOut ? SeatStatus.SittingOut : SeatStatus.Occupied
        };

        seats[seatIndex] = updatedSeat;

        return Task.FromResult<(bool, bool?, string?)>((true, sitOut, null));
    }

    public Task<(bool Success, int? OldSeatNumber, int? NewSeatNumber, SeatDto? Seat, bool IsPending, string? Error)> RequestSeatChangeAsync(
        Guid tableId, string playerName, int desiredSeatNumber)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        if (table == null)
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, "Table not found."));
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, "Player name is required."));
        }

        if (desiredSeatNumber < 1 || desiredSeatNumber > table.MaxSeats)
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, $"Seat number must be between 1 and {table.MaxSeats}."));
        }

        if (!_tableSeats.TryGetValue(tableId, out var seats))
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, "Table seats not initialized."));
        }

        var currentSeatIndex = seats.FindIndex(s => s.PlayerName == playerName && (s.Status == SeatStatus.Occupied || s.Status == SeatStatus.SittingOut));
        if (currentSeatIndex == -1)
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, $"Player '{playerName}' is not seated at this table."));
        }

        var currentSeat = seats[currentSeatIndex];
        if (currentSeat.SeatNumber == desiredSeatNumber)
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, "Player is already in the desired seat."));
        }

        var desiredSeatIndex = seats.FindIndex(s => s.SeatNumber == desiredSeatNumber);
        if (desiredSeatIndex == -1)
        {
            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((false, null, null, null, false, "Desired seat not found."));
        }

        var desiredSeat = seats[desiredSeatIndex];

        // Check if player already has a pending seat change request
        var existingRequest = _pendingSeatChanges.FirstOrDefault(r => r.TableId == tableId && r.PlayerName == playerName);
        if (existingRequest != null)
        {
            // Remove the old request
            _pendingSeatChanges.Remove(existingRequest);
        }

        // If desired seat is available, move immediately (between hands)
        // In a real implementation, this would be queued until between hands
        // For simplicity, we'll check if the game is in a "safe" state or queue it
        if (desiredSeat.Status == SeatStatus.Available)
        {
            // Move the player immediately (simulating between-hands scenario)
            var updatedCurrentSeat = new SeatDto(
                SeatNumber: currentSeat.SeatNumber,
                Status: SeatStatus.Available);

            var updatedDesiredSeat = new SeatDto(
                SeatNumber: desiredSeatNumber,
                Status: currentSeat.Status,
                PlayerName: playerName,
                ChipStack: currentSeat.ChipStack);

            seats[currentSeatIndex] = updatedCurrentSeat;
            seats[desiredSeatIndex] = updatedDesiredSeat;

            return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((true, currentSeat.SeatNumber, desiredSeatNumber, updatedDesiredSeat, false, null));
        }

        // Seat is not available, queue the request (pending until seat becomes available between hands)
        var pendingRequest = new PendingSeatChangeRequest(
            tableId,
            playerName,
            currentSeat.SeatNumber,
            desiredSeatNumber,
            DateTime.UtcNow);

        _pendingSeatChanges.Add(pendingRequest);

        return Task.FromResult<(bool, int?, int?, SeatDto?, bool, string?)>((true, currentSeat.SeatNumber, desiredSeatNumber, null, true, null));
    }

    public Task<(bool Success, int? CashoutAmount, string? Error)> StandUpAsync(
        Guid tableId, string playerName)
    {
        var table = _tables.FirstOrDefault(t => t.Id == tableId);
        if (table == null)
        {
            return Task.FromResult<(bool, int?, string?)>((false, null, "Table not found."));
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            return Task.FromResult<(bool, int?, string?)>((false, null, "Player name is required."));
        }

        if (!_tableSeats.TryGetValue(tableId, out var seats))
        {
            return Task.FromResult<(bool, int?, string?)>((false, null, "Table seats not initialized."));
        }

        var seatIndex = seats.FindIndex(s => s.PlayerName == playerName && (s.Status == SeatStatus.Occupied || s.Status == SeatStatus.SittingOut));
        if (seatIndex == -1)
        {
            return Task.FromResult<(bool, int?, string?)>((false, null, $"Player '{playerName}' is not seated at this table."));
        }

        var seat = seats[seatIndex];
        var cashoutAmount = seat.ChipStack;

        // Free the seat
        var updatedSeat = new SeatDto(
            SeatNumber: seat.SeatNumber,
            Status: SeatStatus.Available);

        seats[seatIndex] = updatedSeat;

        // Update table's occupied seats count
        var tableIndex = _tables.FindIndex(t => t.Id == tableId);
        var currentOccupied = seats.Count(s => s.Status == SeatStatus.Occupied || s.Status == SeatStatus.SittingOut);
        _tables[tableIndex] = table with { OccupiedSeats = currentOccupied };

        // Remove any pending seat change requests for this player
        _pendingSeatChanges.RemoveAll(r => r.TableId == tableId && r.PlayerName == playerName);

        return Task.FromResult<(bool, int?, string?)>((true, cashoutAmount, null));
    }

    public Task CleanupExpiredReservationsAsync()
    {
        var now = DateTime.UtcNow;
        foreach (var tableSeats in _tableSeats.Values)
        {
            for (int i = 0; i < tableSeats.Count; i++)
            {
                var seat = tableSeats[i];
                if (seat.Status == SeatStatus.Reserved && seat.ReservedUntil.HasValue && seat.ReservedUntil.Value < now)
                {
                    tableSeats[i] = seat with { Status = SeatStatus.Available, PlayerName = null, ReservedUntil = null };
                }
            }
        }

        return Task.CompletedTask;
    }

    #endregion
}
