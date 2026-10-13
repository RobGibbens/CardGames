using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CardGames.E2E.Infrastructure;

public sealed record SeededPlayer(string Email, string Password, string DisplayName, Guid PlayerId, string UserId);

/// <summary>
///     Writes a deterministic scenario into the reset database.
///     Player.Name is the email because join looks the player up by the signed-in user name,
///     which is the email claim. The seat shows <see cref="SeededPlayer.DisplayName" /> from Identity first name.
/// </summary>
public sealed class ScenarioSeeder : IAsyncDisposable
{
	public const string Password = "Test1234!";
	public const int StartingChips = 100_000;
	public const int SeatedStack = 1_000;

	private readonly ServiceProvider _services;
	private readonly IServiceScope _scope;
	private readonly CardsDbContext _context;
	private readonly UserManager<ApplicationUser> _users;
	private bool _gameTypesSeeded;

	public ScenarioSeeder(string connectionString)
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
		services.AddDbContext<CardsDbContext>(options => options.UseSqlServer(connectionString));
		services.AddIdentityCore<ApplicationUser>(options =>
			{
				options.SignIn.RequireConfirmedAccount = true;
				options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
			})
			.AddEntityFrameworkStores<CardsDbContext>();

		_services = services.BuildServiceProvider();
		_scope = _services.CreateScope();
		_context = _scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		_users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
	}

	public async Task<SeededPlayer> CreatePlayerAsync(string email, string firstName, string lastName)
	{
		var user = new ApplicationUser
		{
			UserName = email,
			Email = email,
			EmailConfirmed = true,
			FirstName = firstName,
			LastName = lastName
		};

		var createResult = await _users.CreateAsync(user, Password);
		if (!createResult.Succeeded)
		{
			var errors = string.Join(" ", createResult.Errors.Select(error => error.Description));
			throw new InvalidOperationException($"Failed to create {email}: {errors}");
		}

		var now = DateTimeOffset.UtcNow;
		var player = new Player
		{
			Id = Guid.CreateVersion7(),
			Name = email,
			Email = email,
			ExternalId = user.Id,
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now,
			LastActiveAt = now
		};
		_context.Players.Add(player);

		_context.PlayerChipAccounts.Add(new PlayerChipAccount
		{
			PlayerId = player.Id,
			Balance = StartingChips,
			CreatedAtUtc = now,
			UpdatedAtUtc = now
		});
		_context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = player.Id,
			Type = PlayerChipLedgerEntryType.Add,
			AmountDelta = StartingChips,
			BalanceAfter = StartingChips,
			OccurredAtUtc = now,
			ReferenceType = "E2E",
			Reason = "End-to-end scenario seed"
		});

		await _context.SaveChangesAsync();
		return new SeededPlayer(email, Password, firstName, player.Id, user.Id);
	}

	public async Task<Guid> CreateOpenHoldEmGameAsync(string name)
	{
		var game = await CreateHoldEmGameAsync(name);
		return game.Id;
	}

	public async Task<Guid> CreateSeatedHoldEmGameAsync(string name, SeededPlayer first, SeededPlayer second)
	{
		var game = await CreateHoldEmGameAsync(name);
		var now = DateTimeOffset.UtcNow;
		_context.GamePlayers.AddRange(
			CreateSeat(game.Id, first.PlayerId, seatIndex: 0, now),
			CreateSeat(game.Id, second.PlayerId, seatIndex: 1, now));
		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at a blind-based Hold'em table. Seat 0 is the host and the starting dealer.
	/// </summary>
	public async Task<Guid> CreateSeatedHoldEmGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int smallBlind,
		int bigBlind,
		IReadOnlyList<int>? stacks = null)
	{
		if (players.Count < 2)
			throw new ArgumentException("Hold'em needs at least two players.", nameof(players));

		stacks ??= Enumerable.Repeat(SeatedStack, players.Count).ToArray();
		if (stacks.Count != players.Count)
			throw new ArgumentException("Provide one stack per seated player.", nameof(stacks));

		var game = await CreateHoldEmGameAsync(name, smallBlind, bigBlind, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now, stacks[seat]));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at a blind-based Hold the Baseball table. Seat 0 is the host and the starting dealer.
	/// </summary>
	public async Task<Guid> CreateSeatedHoldTheBaseballGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int smallBlind,
		int bigBlind,
		IReadOnlyList<int>? stacks = null)
	{
		if (players.Count < 2)
			throw new ArgumentException("Hold the Baseball needs at least two players.", nameof(players));

		stacks ??= Enumerable.Repeat(SeatedStack, players.Count).ToArray();
		if (stacks.Count != players.Count)
			throw new ArgumentException("Provide one stack per seated player.", nameof(stacks));

		var game = await CreateHoldTheBaseballGameAsync(name, smallBlind, bigBlind, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now, stacks[seat]));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at a blind-based Crazy Pineapple table. Seat 0 is the host and the starting dealer.
	/// </summary>
	public async Task<Guid> CreateSeatedCrazyPineappleGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int smallBlind,
		int bigBlind,
		IReadOnlyList<int>? stacks = null)
	{
		if (players.Count < 2)
			throw new ArgumentException("Crazy Pineapple needs at least two players.", nameof(players));

		stacks ??= Enumerable.Repeat(SeatedStack, players.Count).ToArray();
		if (stacks.Count != players.Count)
			throw new ArgumentException("Provide one stack per seated player.", nameof(stacks));

		var game = await CreateCrazyPineappleGameAsync(name, smallBlind, bigBlind, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now, stacks[seat]));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at a blind-based Bob Barker table. Seat 0 is the host and the starting dealer.
	/// </summary>
	public async Task<Guid> CreateSeatedBobBarkerGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int smallBlind,
		int bigBlind,
		IReadOnlyList<int>? stacks = null)
	{
		if (players.Count < 2)
			throw new ArgumentException("Bob Barker needs at least two players.", nameof(players));

		stacks ??= Enumerable.Repeat(SeatedStack, players.Count).ToArray();
		if (stacks.Count != players.Count)
			throw new ArgumentException("Provide one stack per seated player.", nameof(stacks));

		var game = await CreateBobBarkerGameAsync(name, smallBlind, bigBlind, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now, stacks[seat]));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at an In-Between table. Seat 0 is the host and the starting dealer.
	///     The only stake is the ante that builds each hand's pot.
	/// </summary>
	public async Task<Guid> CreateSeatedInBetweenGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int ante,
		IReadOnlyList<int>? stacks = null)
	{
		if (players.Count < 2)
			throw new ArgumentException("In-Between needs at least two players.", nameof(players));

		stacks ??= Enumerable.Repeat(SeatedStack, players.Count).ToArray();
		if (stacks.Count != players.Count)
			throw new ArgumentException("Provide one stack per seated player.", nameof(stacks));

		var game = await CreateInBetweenGameAsync(name, ante, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now, stacks[seat]));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at a Baseball table. Seat 0 is the host and the starting dealer.
	///     Stakes are an ante, a bring-in, and fixed-limit small and big bets. The buy price is the minimum bet.
	/// </summary>
	public async Task<Guid> CreateSeatedBaseballGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int ante,
		int bringIn,
		int smallBet,
		int bigBet,
		int buyCardPrice)
	{
		if (players.Count < 2)
			throw new ArgumentException("Baseball needs at least two players.", nameof(players));

		var game = await CreateBaseballGameAsync(name, ante, bringIn, smallBet, bigBet, buyCardPrice, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at a Follow the Queen table. Seat 0 is the host and the starting dealer.
	///     Stakes are an ante, a bring-in, and fixed-limit small and big bets.
	/// </summary>
	public async Task<Guid> CreateSeatedFollowTheQueenGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int ante,
		int bringIn,
		int smallBet,
		int bigBet)
	{
		if (players.Count < 2)
			throw new ArgumentException("Follow the Queen needs at least two players.", nameof(players));

		var game = await CreateFollowTheQueenGameAsync(name, ante, bringIn, smallBet, bigBet, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	/// <summary>
	///     Seats every player at an ante Five Card Draw table. Seat 0 is the host and the starting dealer.
	/// </summary>
	public async Task<Guid> CreateSeatedFiveCardDrawGameAsync(
		string name,
		IReadOnlyList<SeededPlayer> players,
		int ante,
		int minBet)
	{
		if (players.Count < 2)
			throw new ArgumentException("Five Card Draw needs at least two players.", nameof(players));

		var game = await CreateFiveCardDrawGameAsync(name, ante, minBet, players[0]);
		var now = DateTimeOffset.UtcNow;
		for (var seat = 0; seat < players.Count; seat++)
			_context.GamePlayers.Add(CreateSeat(game.Id, players[seat].PlayerId, seat, now));

		await _context.SaveChangesAsync();
		return game.Id;
	}

	public async Task<int> GetCashierBalanceAsync(Guid playerId)
	{
		var account = await _context.PlayerChipAccounts
			.AsNoTracking()
			.SingleAsync(account => account.PlayerId == playerId);
		return account.Balance;
	}

	public async ValueTask DisposeAsync()
	{
		_scope.Dispose();
		await _services.DisposeAsync();
	}

	private async Task<Game> CreateHoldEmGameAsync(
		string name,
		int? smallBlind = null,
		int? bigBlind = null,
		SeededPlayer? host = null)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "HOLDEM");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = smallBlind is null ? 10 : 0,
			MinBet = bigBlind ?? 10,
			SmallBlind = smallBlind,
			BigBlind = bigBlind,
			CreatedById = host?.UserId,
			CreatedByName = host?.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateHoldTheBaseballGameAsync(
		string name,
		int smallBlind,
		int bigBlind,
		SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "HOLDTHEBASEBALL");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = 0,
			MinBet = bigBlind,
			SmallBlind = smallBlind,
			BigBlind = bigBlind,
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateCrazyPineappleGameAsync(
		string name,
		int smallBlind,
		int bigBlind,
		SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "CRAZYPINEAPPLE");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = 0,
			MinBet = bigBlind,
			SmallBlind = smallBlind,
			BigBlind = bigBlind,
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateBobBarkerGameAsync(
		string name,
		int smallBlind,
		int bigBlind,
		SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "BOBBARKER");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = 0,
			MinBet = bigBlind,
			SmallBlind = smallBlind,
			BigBlind = bigBlind,
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateInBetweenGameAsync(string name, int ante, SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "INBETWEEN");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = ante,
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateBaseballGameAsync(
		string name,
		int ante,
		int bringIn,
		int smallBet,
		int bigBet,
		int buyCardPrice,
		SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "BASEBALL");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = ante,
			BringIn = bringIn,
			SmallBet = smallBet,
			BigBet = bigBet,
			MinBet = buyCardPrice,
			GameSettings = $"{{\"buyCardPrice\":{buyCardPrice}}}",
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateFollowTheQueenGameAsync(
		string name,
		int ante,
		int bringIn,
		int smallBet,
		int bigBet,
		SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "FOLLOWTHEQUEEN");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = ante,
			BringIn = bringIn,
			SmallBet = smallBet,
			BigBet = bigBet,
			MinBet = smallBet,
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private async Task<Game> CreateFiveCardDrawGameAsync(
		string name,
		int ante,
		int minBet,
		SeededPlayer host)
	{
		await EnsureGameTypesAsync();
		var gameType = await _context.GameTypes.SingleAsync(type => type.Code == "FIVECARDDRAW");
		var now = DateTimeOffset.UtcNow;
		var game = new Game
		{
			Id = Guid.CreateVersion7(),
			GameTypeId = gameType.Id,
			Name = name,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = ante,
			MinBet = minBet,
			CreatedById = host.UserId,
			CreatedByName = host.Email,
			Status = GameStatus.WaitingForPlayers,
			RequiresJoinApproval = false,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedAt = now,
			UpdatedAt = now
		};
		_context.Games.Add(game);
		await _context.SaveChangesAsync();
		return game;
	}

	private static GamePlayer CreateSeat(
		Guid gameId, Guid playerId, int seatIndex, DateTimeOffset joinedAt, int? chipStack = null)
	{
		var stack = chipStack ?? SeatedStack;
		return new GamePlayer
		{
			Id = Guid.CreateVersion7(),
			GameId = gameId,
			PlayerId = playerId,
			SeatPosition = seatIndex,
			ChipStack = stack,
			StartingChips = stack,
			Status = GamePlayerStatus.Active,
			IsConnected = true,
			JoinedAt = joinedAt
		};
	}

	private async Task EnsureGameTypesAsync()
	{
		if (_gameTypesSeeded)
			return;

		var now = DateTimeOffset.UtcNow;
		var gameTypes = new List<GameType>
		{
			CreateGameType("FIVECARDDRAW", "Five Card Draw", 2, 8, 5, 0, 0, 5, now),
			CreateGameType("SEVENCARDSTUD", "Seven Card Stud", 2, 8, 2, 1, 0, 7, now),
			CreateGameType("RAZZ", "Razz", 2, 8, 2, 1, 0, 7, now),
			CreateGameType("KINGSANDLOWS", "Kings and Lows", 2, 8, 5, 0, 0, 5, now),
			CreateGameType("TWOSJACKSMANWITHTHEAXE", "Twos, Jacks, and Man with the Axe", 2, 8, 5, 0, 0, 5, now),
			CreateGameType("HOLDEM", "Texas Hold'em", 2, 10, 2, 0, 5, 7, now),
			CreateGameType("CRAZYPINEAPPLE", "Crazy Pineapple", 2, 10, 3, 0, 5, 8, now),
			CreateGameType("BOBBARKER", "Bob Barker", 2, 10, 5, 0, 6, 11, now),
			CreateGameType("HOLDTHEBASEBALL", "Hold the Baseball", 2, 10, 2, 0, 5, 7, now),
			CreateGameType("OMAHA", "Omaha", 2, 10, 4, 0, 5, 9, now),
			CreateGameType("NEBRASKA", "Nebraska", 2, 10, 5, 0, 5, 10, now),
			CreateGameType("SOUTHDAKOTA", "South Dakota", 2, 10, 5, 0, 4, 9, now),
			CreateGameType("IRISHHOLDEM", "Irish Hold 'Em", 2, 10, 4, 0, 5, 9, now),
			CreateGameType("FOLLOWTHEQUEEN", "Follow the Queen", 2, 8, 2, 1, 0, 7, now),
			CreateGameType("BASEBALL", "Baseball", 2, 8, 2, 1, 0, 7, now),
			CreateGameType("GOODBADUGLY", "The Good, the Bad, and the Ugly", 2, 7, 2, 1, 0, 7, now),
			CreateGameType("SCREWYOURNEIGHBOR", "Screw Your Neighbor", 3, 10, 1, 0, 0, 1, now),
			CreateGameType("KLONDIKE", "Klondike", 2, 10, 2, 0, 6, 2, now),
			CreateGameType("INBETWEEN", "In-Between", 2, 20, 0, 0, 3, 0, now)
		};

		await _context.GameTypes.AddRangeAsync(gameTypes);
		await _context.SaveChangesAsync();
		_gameTypesSeeded = true;
	}

	private static GameType CreateGameType(
		string code, string name, int minPlayers, int maxPlayers,
		int holeCards, int boardCards, int communityCards, int maxPlayerCards,
		DateTimeOffset now)
	{
		return new GameType
		{
			Id = Guid.CreateVersion7(),
			Code = code,
			Name = name,
			MinPlayers = minPlayers,
			MaxPlayers = maxPlayers,
			InitialHoleCards = holeCards,
			InitialBoardCards = boardCards,
			MaxCommunityCards = communityCards,
			MaxPlayerCards = maxPlayerCards,
			BettingStructure = BettingStructure.Ante,
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};
	}
}
