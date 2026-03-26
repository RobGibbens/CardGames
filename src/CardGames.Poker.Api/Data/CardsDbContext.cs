using CardGames.Poker.Api.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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
public class CardsDbContext(DbContextOptions<CardsDbContext> options) : IdentityDbContext<ApplicationUser>(options)
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
	/// Gets the set of game join requests.
	/// </summary>
	public DbSet<GameJoinRequest> GameJoinRequests => Set<GameJoinRequest>();

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

	/// <summary>
	/// Gets the set of hand history records.
	/// </summary>
	public DbSet<HandHistory> HandHistories => Set<HandHistory>();

	/// <summary>
	/// Gets the set of hand history winners.
	/// </summary>
	public DbSet<HandHistoryWinner> HandHistoryWinners => Set<HandHistoryWinner>();

	/// <summary>
	/// Gets the set of hand history player results.
	/// </summary>
	public DbSet<HandHistoryPlayerResult> HandHistoryPlayerResults => Set<HandHistoryPlayerResult>();

	/// <summary>
	/// Gets the set of dealer's choice hand log entries.
	/// </summary>
	public DbSet<DealersChoiceHandLog> DealersChoiceHandLogs => Set<DealersChoiceHandLog>();

	/// <summary>
	/// Gets the set of account-level chip balances for players.
	/// </summary>
	public DbSet<PlayerChipAccount> PlayerChipAccounts => Set<PlayerChipAccount>();

	/// <summary>
	/// Gets the set of account-level chip ledger entries.
	/// </summary>
	public DbSet<PlayerChipLedgerEntry> PlayerChipLedgerEntries => Set<PlayerChipLedgerEntry>();

	/// <summary>
	/// Gets the set of leagues.
	/// </summary>
	public DbSet<League> Leagues => Set<League>();

	/// <summary>
	/// Gets the set of current league members.
	/// </summary>
	public DbSet<LeagueMemberCurrent> LeagueMembersCurrent => Set<LeagueMemberCurrent>();

	/// <summary>
	/// Gets the set of league membership events.
	/// </summary>
	public DbSet<LeagueMembershipEvent> LeagueMembershipEvents => Set<LeagueMembershipEvent>();

	/// <summary>
	/// Gets the set of per-user game preference defaults.
	/// </summary>
	public DbSet<UserGamePreferences> UserGamePreferences => Set<UserGamePreferences>();

	/// <summary>
	/// Gets the set of league invites.
	/// </summary>
	public DbSet<LeagueInvite> LeagueInvites => Set<LeagueInvite>();

	/// <summary>
	/// Gets the set of league join requests.
	/// </summary>
	public DbSet<LeagueJoinRequest> LeagueJoinRequests => Set<LeagueJoinRequest>();

	/// <summary>
	/// Gets the set of league seasons.
	/// </summary>
	public DbSet<LeagueSeason> LeagueSeasons => Set<LeagueSeason>();

	/// <summary>
	/// Gets the set of league season events.
	/// </summary>
	public DbSet<LeagueSeasonEvent> LeagueSeasonEvents => Set<LeagueSeasonEvent>();

	/// <summary>
	/// Gets the set of league one-off events.
	/// </summary>
	public DbSet<LeagueOneOffEvent> LeagueOneOffEvents => Set<LeagueOneOffEvent>();

	/// <summary>
	/// Gets the set of league season event results.
	/// </summary>
	public DbSet<LeagueSeasonEventResult> LeagueSeasonEventResults => Set<LeagueSeasonEventResult>();

	/// <summary>
	/// Gets the set of league season event result correction audit entries.
	/// </summary>
	public DbSet<LeagueSeasonEventResultCorrectionAudit> LeagueSeasonEventResultCorrectionAudits => Set<LeagueSeasonEventResultCorrectionAudit>();

	/// <summary>
	/// Gets the set of current league standings.
	/// </summary>
	public DbSet<LeagueStandingCurrent> LeagueStandingsCurrent => Set<LeagueStandingCurrent>();

	protected override void OnModelCreating(ModelBuilder model)
	{
		base.OnModelCreating(model);
		model.ApplyConfigurationsFromAssembly(typeof(CardsDbContext).Assembly);
	}
}