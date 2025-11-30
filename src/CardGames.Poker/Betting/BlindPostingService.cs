#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Betting;

/// <summary>
/// Represents a posted blind or ante.
/// </summary>
public class PostedBlind
{
    /// <summary>
    /// The player who posted the blind.
    /// </summary>
    public string PlayerName { get; init; } = string.Empty;

    /// <summary>
    /// The seat number of the player.
    /// </summary>
    public int SeatNumber { get; init; }

    /// <summary>
    /// The type of blind posted.
    /// </summary>
    public BlindType Type { get; init; }

    /// <summary>
    /// The amount posted.
    /// </summary>
    public int Amount { get; init; }

    /// <summary>
    /// Whether this was a dead blind (doesn't count towards call amount).
    /// </summary>
    public bool IsDead { get; init; }
}

/// <summary>
/// The type of blind being posted.
/// </summary>
public enum BlindType
{
    /// <summary>Small blind.</summary>
    SmallBlind,

    /// <summary>Big blind.</summary>
    BigBlind,

    /// <summary>Ante.</summary>
    Ante,

    /// <summary>Bring-in (for stud games).</summary>
    BringIn,

    /// <summary>Missed small blind that must be posted.</summary>
    MissedSmallBlind,

    /// <summary>Missed big blind that must be posted.</summary>
    MissedBigBlind,

    /// <summary>Button straddle.</summary>
    Straddle
}

/// <summary>
/// Tracks missed blinds for a player.
/// </summary>
public class MissedBlindInfo
{
    /// <summary>
    /// The player name.
    /// </summary>
    public string PlayerName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the player missed the small blind.
    /// </summary>
    public bool MissedSmallBlind { get; set; }

    /// <summary>
    /// Whether the player missed the big blind.
    /// </summary>
    public bool MissedBigBlind { get; set; }

    /// <summary>
    /// The hand number when blinds were missed.
    /// </summary>
    public int HandNumberMissed { get; set; }

    /// <summary>
    /// The actual small blind amount that was missed.
    /// </summary>
    public int MissedSmallBlindAmount { get; set; }

    /// <summary>
    /// The actual big blind amount that was missed.
    /// </summary>
    public int MissedBigBlindAmount { get; set; }

    /// <summary>
    /// Total amount of missed blinds that must be posted.
    /// </summary>
    public int TotalMissedAmount => MissedSmallBlindAmount + MissedBigBlindAmount;
}

/// <summary>
/// Result of posting blinds for a hand.
/// </summary>
public class BlindPostingResult
{
    /// <summary>
    /// Whether the blind posting was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if posting failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of all blinds that were posted.
    /// </summary>
    public IReadOnlyList<PostedBlind> PostedBlinds { get; init; } = Array.Empty<PostedBlind>();

    /// <summary>
    /// The total amount collected in blinds.
    /// </summary>
    public int TotalCollected { get; init; }
}

/// <summary>
/// Result of collecting antes for a hand.
/// </summary>
public class AnteCollectionResult
{
    /// <summary>
    /// Whether the ante collection was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if collection failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of all antes that were posted.
    /// </summary>
    public IReadOnlyList<PostedBlind> PostedAntes { get; init; } = Array.Empty<PostedBlind>();

    /// <summary>
    /// The total amount collected in antes.
    /// </summary>
    public int TotalCollected { get; init; }
}

/// <summary>
/// Service for handling blind and ante posting.
/// </summary>
public class BlindPostingService
{
    private readonly Dictionary<string, MissedBlindInfo> _missedBlinds = new();

    /// <summary>
    /// Posts small and big blinds based on button position.
    /// </summary>
    /// <param name="players">List of players at the table.</param>
    /// <param name="smallBlindPosition">The seat position for the small blind.</param>
    /// <param name="bigBlindPosition">The seat position for the big blind.</param>
    /// <param name="smallBlindAmount">The small blind amount.</param>
    /// <param name="bigBlindAmount">The big blind amount.</param>
    /// <param name="potManager">The pot manager for the hand.</param>
    /// <returns>Result of the blind posting.</returns>
    public BlindPostingResult PostBlinds(
        IReadOnlyList<PokerPlayer> players,
        int smallBlindPosition,
        int bigBlindPosition,
        int smallBlindAmount,
        int bigBlindAmount,
        PotManager potManager)
    {
        if (players == null || players.Count < 2)
        {
            return new BlindPostingResult
            {
                Success = false,
                ErrorMessage = "At least 2 players are required to post blinds."
            };
        }

        var postedBlinds = new List<PostedBlind>();
        var totalCollected = 0;

        // Find players by position
        var smallBlindPlayer = players.FirstOrDefault(p => GetPlayerSeatPosition(p, players) == smallBlindPosition);
        var bigBlindPlayer = players.FirstOrDefault(p => GetPlayerSeatPosition(p, players) == bigBlindPosition);

        if (smallBlindPlayer == null || bigBlindPlayer == null)
        {
            return new BlindPostingResult
            {
                Success = false,
                ErrorMessage = "Could not find players at blind positions."
            };
        }

        // Post small blind
        var sbActual = Math.Min(smallBlindAmount, smallBlindPlayer.ChipStack);
        if (sbActual > 0)
        {
            var sbPosted = smallBlindPlayer.PlaceBet(sbActual);
            potManager.AddContribution(smallBlindPlayer.Name, sbPosted);
            totalCollected += sbPosted;

            postedBlinds.Add(new PostedBlind
            {
                PlayerName = smallBlindPlayer.Name,
                SeatNumber = smallBlindPosition,
                Type = BlindType.SmallBlind,
                Amount = sbPosted,
                IsDead = false
            });
        }

        // Post big blind
        var bbActual = Math.Min(bigBlindAmount, bigBlindPlayer.ChipStack);
        if (bbActual > 0)
        {
            var bbPosted = bigBlindPlayer.PlaceBet(bbActual);
            potManager.AddContribution(bigBlindPlayer.Name, bbPosted);
            totalCollected += bbPosted;

            postedBlinds.Add(new PostedBlind
            {
                PlayerName = bigBlindPlayer.Name,
                SeatNumber = bigBlindPosition,
                Type = BlindType.BigBlind,
                Amount = bbPosted,
                IsDead = false
            });
        }

        return new BlindPostingResult
        {
            Success = true,
            PostedBlinds = postedBlinds,
            TotalCollected = totalCollected
        };
    }

    /// <summary>
    /// Collects antes from all players.
    /// </summary>
    /// <param name="players">List of players at the table.</param>
    /// <param name="seatNumbers">The seat numbers corresponding to each player.</param>
    /// <param name="anteAmount">The ante amount per player.</param>
    /// <param name="potManager">The pot manager for the hand.</param>
    /// <returns>Result of the ante collection.</returns>
    public AnteCollectionResult CollectAntes(
        IReadOnlyList<PokerPlayer> players,
        IReadOnlyList<int> seatNumbers,
        int anteAmount,
        PotManager potManager)
    {
        if (players == null || players.Count == 0)
        {
            return new AnteCollectionResult
            {
                Success = false,
                ErrorMessage = "No players to collect antes from."
            };
        }

        if (seatNumbers == null || seatNumbers.Count != players.Count)
        {
            return new AnteCollectionResult
            {
                Success = false,
                ErrorMessage = "Seat numbers must match the number of players."
            };
        }

        if (anteAmount <= 0)
        {
            return new AnteCollectionResult
            {
                Success = true,
                PostedAntes = Array.Empty<PostedBlind>(),
                TotalCollected = 0
            };
        }

        var postedAntes = new List<PostedBlind>();
        var totalCollected = 0;

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var seatNumber = seatNumbers[i];
            var actualAnte = Math.Min(anteAmount, player.ChipStack);

            if (actualAnte > 0)
            {
                var antePosted = player.PlaceBet(actualAnte);
                potManager.AddContribution(player.Name, antePosted);
                totalCollected += antePosted;

                postedAntes.Add(new PostedBlind
                {
                    PlayerName = player.Name,
                    SeatNumber = seatNumber,
                    Type = BlindType.Ante,
                    Amount = antePosted,
                    IsDead = false
                });
            }
        }

        // Reset current bets after collecting antes (antes don't count towards calling)
        foreach (var player in players)
        {
            player.ResetCurrentBet();
        }

        return new AnteCollectionResult
        {
            Success = true,
            PostedAntes = postedAntes,
            TotalCollected = totalCollected
        };
    }

    /// <summary>
    /// Collects antes from all players. Uses array index as seat number.
    /// For games where seat numbers match player index.
    /// </summary>
    /// <param name="players">List of players at the table.</param>
    /// <param name="anteAmount">The ante amount per player.</param>
    /// <param name="potManager">The pot manager for the hand.</param>
    /// <returns>Result of the ante collection.</returns>
    public AnteCollectionResult CollectAntes(
        IReadOnlyList<PokerPlayer> players,
        int anteAmount,
        PotManager potManager)
    {
        if (players == null || players.Count == 0)
        {
            return new AnteCollectionResult
            {
                Success = false,
                ErrorMessage = "No players to collect antes from."
            };
        }

        // Default seat numbers to array indices
        var seatNumbers = Enumerable.Range(0, players.Count).ToList();
        return CollectAntes(players, seatNumbers, anteAmount, potManager);
    }

    /// <summary>
    /// Records that a player has missed blinds.
    /// </summary>
    /// <param name="playerName">The player who missed blinds.</param>
    /// <param name="missedSmallBlind">Whether they missed the small blind.</param>
    /// <param name="missedBigBlind">Whether they missed the big blind.</param>
    /// <param name="smallBlindAmount">The small blind amount.</param>
    /// <param name="bigBlindAmount">The big blind amount.</param>
    /// <param name="handNumber">The current hand number.</param>
    public void RecordMissedBlinds(
        string playerName,
        bool missedSmallBlind,
        bool missedBigBlind,
        int smallBlindAmount,
        int bigBlindAmount,
        int handNumber)
    {
        if (!_missedBlinds.TryGetValue(playerName, out var info))
        {
            info = new MissedBlindInfo { PlayerName = playerName };
            _missedBlinds[playerName] = info;
        }

        if (missedSmallBlind && !info.MissedSmallBlind)
        {
            info.MissedSmallBlind = true;
            info.MissedSmallBlindAmount = smallBlindAmount;
        }

        if (missedBigBlind && !info.MissedBigBlind)
        {
            info.MissedBigBlind = true;
            info.MissedBigBlindAmount = bigBlindAmount;
        }

        info.HandNumberMissed = handNumber;
    }

    /// <summary>
    /// Gets the missed blind information for a player.
    /// </summary>
    /// <param name="playerName">The player name.</param>
    /// <returns>The missed blind info, or null if none.</returns>
    public MissedBlindInfo? GetMissedBlinds(string playerName)
    {
        return _missedBlinds.TryGetValue(playerName, out var info) ? info : null;
    }

    /// <summary>
    /// Posts missed blinds for a returning player.
    /// </summary>
    /// <param name="player">The returning player.</param>
    /// <param name="seatNumber">The player's seat number.</param>
    /// <param name="potManager">The pot manager for the hand.</param>
    /// <returns>List of posted blinds.</returns>
    public IReadOnlyList<PostedBlind> PostMissedBlinds(
        PokerPlayer player,
        int seatNumber,
        PotManager potManager)
    {
        var info = GetMissedBlinds(player.Name);
        if (info == null)
        {
            return Array.Empty<PostedBlind>();
        }

        var postedBlinds = new List<PostedBlind>();

        // Post missed small blind as a dead blind (goes directly to pot, doesn't count as bet)
        if (info.MissedSmallBlind)
        {
            var deadAmount = Math.Min(info.MissedSmallBlindAmount, player.ChipStack);
            if (deadAmount > 0)
            {
                var posted = player.PlaceBet(deadAmount);
                potManager.AddContribution(player.Name, posted);

                postedBlinds.Add(new PostedBlind
                {
                    PlayerName = player.Name,
                    SeatNumber = seatNumber,
                    Type = BlindType.MissedSmallBlind,
                    Amount = posted,
                    IsDead = true
                });

                // Reset current bet since this is a dead blind
                player.ResetCurrentBet();
            }
        }

        // Post missed big blind as a live blind
        if (info.MissedBigBlind)
        {
            var liveAmount = Math.Min(info.MissedBigBlindAmount, player.ChipStack);
            if (liveAmount > 0)
            {
                var posted = player.PlaceBet(liveAmount);
                potManager.AddContribution(player.Name, posted);

                postedBlinds.Add(new PostedBlind
                {
                    PlayerName = player.Name,
                    SeatNumber = seatNumber,
                    Type = BlindType.MissedBigBlind,
                    Amount = posted,
                    IsDead = false
                });
            }
        }

        // Clear the missed blinds record
        _missedBlinds.Remove(player.Name);

        return postedBlinds;
    }

    /// <summary>
    /// Checks if a player has missed blinds that need to be posted.
    /// </summary>
    /// <param name="playerName">The player name.</param>
    /// <returns>True if the player has missed blinds.</returns>
    public bool HasMissedBlinds(string playerName)
    {
        return _missedBlinds.ContainsKey(playerName);
    }

    /// <summary>
    /// Clears all missed blind records.
    /// </summary>
    public void ClearAllMissedBlinds()
    {
        _missedBlinds.Clear();
    }

    /// <summary>
    /// Gets all players with missed blinds.
    /// </summary>
    /// <returns>Dictionary of player names to missed blind info.</returns>
    public IReadOnlyDictionary<string, MissedBlindInfo> GetAllMissedBlinds()
    {
        return _missedBlinds;
    }

    private static int GetPlayerSeatPosition(PokerPlayer player, IReadOnlyList<PokerPlayer> players)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Name == player.Name)
            {
                return i;
            }
        }
        return -1;
    }
}
