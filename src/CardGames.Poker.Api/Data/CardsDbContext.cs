using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Data;

/// <summary>
/// Entity Framework Core database context for the CardGames poker application.
/// </summary>
/// <remarks>
/// <para>
/// This context provides access to all poker-related entities including:
/// </para>
/// <list type="bullet">
///   <item><description>Game types and their configurations</description></item>
///   <item><description>Game sessions and their state</description></item>
///   <item><description>Players and their participation in games</description></item>
///   <item><description>Cards dealt and their positions</description></item>
///   <item><description>Pots and player contributions</description></item>
///   <item><description>Betting rounds and action history</description></item>
/// </list>
/// <para>
/// Entity configurations are applied automatically from the assembly.
/// </para>
/// </remarks>
public class CardsDbContext(DbContextOptions<CardsDbContext> options) : DbContext(options)
{
	/// <summary>
	/// Gets the set of game type definitions.
	/// </summary>
	public DbSet<GameType> GameTypes => Set<GameType>();

	/// <summary>
	/// Gets the set of game sessions.
	/// </summary>
	public DbSet<Game> Games => Set<Game>();

	/// <summary>
	/// Gets the set of player identities.
	/// </summary>
	public DbSet<Player> Players => Set<Player>();

	/// <summary>
	/// Gets the set of player participations in games.
	/// </summary>
	public DbSet<GamePlayer> GamePlayers => Set<GamePlayer>();

	/// <summary>
	/// Gets the set of cards in games.
	/// </summary>
	public DbSet<GameCard> GameCards => Set<GameCard>();

	/// <summary>
	/// Gets the set of pots in games.
	/// </summary>
	public DbSet<Pot> Pots => Set<Pot>();

	/// <summary>
	/// Gets the set of pot contributions.
	/// </summary>
	public DbSet<PotContribution> PotContributions => Set<PotContribution>();

	/// <summary>
	/// Gets the set of betting rounds.
	/// </summary>
	public DbSet<BettingRound> BettingRounds => Set<BettingRound>();

	/// <summary>
	/// Gets the set of betting action records.
	/// </summary>
	public DbSet<BettingActionRecord> BettingActionRecords => Set<BettingActionRecord>();

	protected override void OnModelCreating(ModelBuilder model)
	{
		model.ApplyConfigurationsFromAssembly(typeof(CardsDbContext).Assembly);
	}
}