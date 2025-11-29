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
}

public class InMemoryTablesRepository : ITablesRepository
{
    private readonly List<TableSummaryDto> _tables;

    public InMemoryTablesRepository()
    {
        // Seed with sample tables for demonstration
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
                Id: Guid.NewGuid(),
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

        return Task.FromResult(table);
    }
}
