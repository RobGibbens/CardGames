using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Infrastructure;

/// <summary>
/// Helper class for seeding test data consistently across integration tests.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Creates a game with the specified game type and optional configuration.
    /// </summary>
    public static async Task<Game> CreateGameAsync(
        CardsDbContext context,
        string gameTypeCode,
        int? ante = 10,
        int? minBet = 10,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var gameType = await context.GameTypes
            .FirstOrDefaultAsync(gt => gt.Code == gameTypeCode, cancellationToken)
            ?? throw new InvalidOperationException($"GameType '{gameTypeCode}' not found. Ensure SeedBaseDataAsync was called.");

        var game = new Game
        {
            Id = Guid.CreateVersion7(),
            GameTypeId = gameType.Id,
            GameType = gameType,
            Name = name ?? $"Test {gameType.Name} Game",
            CurrentPhase = nameof(Phases.WaitingToStart),
            CurrentHandNumber = 0,
            DealerPosition = 0,
            Ante = ante,
            MinBet = minBet,
            Status = GameStatus.WaitingForPlayers,
            CurrentPlayerIndex = -1,
            CurrentDrawPlayerIndex = -1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);

        return game;
    }

    /// <summary>
    /// Creates a player with the specified name.
    /// </summary>
    public static async Task<Player> CreatePlayerAsync(
        CardsDbContext context,
        string name,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var player = new Player
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Email = email ?? $"{name.ToLowerInvariant().Replace(" ", ".")}@test.com"
        };

        context.Players.Add(player);
        await context.SaveChangesAsync(cancellationToken);

        return player;
    }

    /// <summary>
    /// Adds a player to a game at the specified seat with initial chips.
    /// </summary>
    public static async Task<GamePlayer> AddPlayerToGameAsync(
        CardsDbContext context,
        Game game,
        Player player,
        int seatPosition,
        int startingChips = 1000,
        CancellationToken cancellationToken = default)
    {
        var gamePlayer = new GamePlayer
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            Game = game,
            PlayerId = player.Id,
            Player = player,
            SeatPosition = seatPosition,
            ChipStack = startingChips,
            StartingChips = startingChips,
            CurrentBet = 0,
            TotalContributedThisHand = 0,
            HasFolded = false,
            IsAllIn = false,
            IsSittingOut = false,
            Status = GamePlayerStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow
        };

        context.GamePlayers.Add(gamePlayer);
        await context.SaveChangesAsync(cancellationToken);

        return gamePlayer;
    }

    /// <summary>
    /// Creates a complete game setup with the specified number of players.
    /// </summary>
    public static async Task<GameSetup> CreateCompleteGameSetupAsync(
        CardsDbContext context,
        string gameTypeCode,
        int numberOfPlayers,
        int startingChips = 1000,
        int ante = 10,
        CancellationToken cancellationToken = default)
    {
        var game = await CreateGameAsync(context, gameTypeCode, ante, cancellationToken: cancellationToken);
        var players = new List<Player>();
        var gamePlayers = new List<GamePlayer>();

        for (var i = 0; i < numberOfPlayers; i++)
        {
            var player = await CreatePlayerAsync(context, $"Player {i + 1}", cancellationToken: cancellationToken);
            var gamePlayer = await AddPlayerToGameAsync(
                context, game, player, i, startingChips, cancellationToken);
            
            players.Add(player);
            gamePlayers.Add(gamePlayer);
        }

        // Reload game with all relationships
        var loadedGame = await context.Games
            .Include(g => g.GameType)
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == game.Id, cancellationToken);

        return new GameSetup(loadedGame, players, gamePlayers);
    }

    /// <summary>
    /// Creates a pot for the current hand of a game.
    /// </summary>
    public static async Task<CardGames.Poker.Api.Data.Entities.Pot> CreatePotAsync(
        CardsDbContext context,
        Game game,
        int amount,
        PotType potType = PotType.Main,
        CancellationToken cancellationToken = default)
    {
        var pot = new CardGames.Poker.Api.Data.Entities.Pot
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = potType,
            PotOrder = 0,
            Amount = amount,
            IsAwarded = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Pots.Add(pot);
        await context.SaveChangesAsync(cancellationToken);

        return pot;
    }
}

/// <summary>
/// Represents a complete game setup with game, players, and game players.
/// </summary>
public record GameSetup(Game Game, List<Player> Players, List<GamePlayer> GamePlayers);
