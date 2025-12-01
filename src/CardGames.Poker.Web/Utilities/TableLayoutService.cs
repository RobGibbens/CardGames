namespace CardGames.Poker.Web.Utilities;

/// <summary>
/// Represents a player's position on the table.
/// </summary>
public record PlayerLayoutPosition(
    int SeatNumber,
    double Top,
    double Left,
    double BetTop,
    double BetLeft,
    string Alignment);

/// <summary>
/// Service for computing player positions around a poker table.
/// Supports 2-10 player configurations.
/// </summary>
public static class TableLayoutService
{
    /// <summary>
    /// Gets the positions for all seats based on the player count.
    /// </summary>
    /// <param name="playerCount">Number of players (2-10).</param>
    /// <returns>Collection of player positions.</returns>
    public static IReadOnlyList<PlayerLayoutPosition> GetPositions(int playerCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerCount, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(playerCount, 10);

        return playerCount switch
        {
            2 => GetHeadsUpPositions(),
            3 => Get3MaxPositions(),
            4 => Get4MaxPositions(),
            5 => Get5MaxPositions(),
            6 => Get6MaxPositions(),
            7 => Get7MaxPositions(),
            8 => Get8MaxPositions(),
            9 => Get9MaxPositions(),
            10 => Get10MaxPositions(),
            // This case is unreachable due to the validation above, but included for exhaustiveness
            _ => throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, "Player count must be between 2 and 10")
        };
    }

    /// <summary>
    /// Gets the position for a specific seat.
    /// </summary>
    public static PlayerLayoutPosition GetPosition(int seatNumber, int totalSeats)
    {
        var positions = GetPositions(totalSeats);
        return positions.FirstOrDefault(p => p.SeatNumber == seatNumber) 
            ?? positions.First();
    }

    /// <summary>
    /// Gets the center position for the pot display.
    /// </summary>
    public static (double Top, double Left) GetPotPosition() => (50, 50);

    /// <summary>
    /// Gets the position for community cards.
    /// </summary>
    public static (double Top, double Left) GetCommunityCardsPosition() => (45, 50);

    /// <summary>
    /// Calculates the bet position offset from the player position.
    /// </summary>
    public static (double BetTop, double BetLeft) CalculateBetPosition(double playerTop, double playerLeft)
    {
        // Move bet positions toward center of table
        var betTop = playerTop + (50 - playerTop) * 0.35;
        var betLeft = playerLeft + (50 - playerLeft) * 0.35;
        return (betTop, betLeft);
    }

    // Heads-up (2 players)
    private static IReadOnlyList<PlayerLayoutPosition> GetHeadsUpPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom (hero)
            new(2, 12, 50, 28, 50, "center")   // Top (opponent)
        ];
    }

    // 3-max
    private static IReadOnlyList<PlayerLayoutPosition> Get3MaxPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom
            new(2, 30, 15, 38, 28, "left"),    // Top left
            new(3, 30, 85, 38, 72, "right")    // Top right
        ];
    }

    // 4-max
    private static IReadOnlyList<PlayerLayoutPosition> Get4MaxPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom
            new(2, 50, 8, 50, 25, "left"),     // Left
            new(3, 12, 50, 28, 50, "center"),  // Top
            new(4, 50, 92, 50, 75, "right")    // Right
        ];
    }

    // 5-max
    private static IReadOnlyList<PlayerLayoutPosition> Get5MaxPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom
            new(2, 65, 8, 58, 25, "left"),     // Bottom left
            new(3, 20, 20, 32, 32, "left"),    // Top left
            new(4, 20, 80, 32, 68, "right"),   // Top right
            new(5, 65, 92, 58, 75, "right")    // Bottom right
        ];
    }

    // 6-max
    private static IReadOnlyList<PlayerLayoutPosition> Get6MaxPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom
            new(2, 65, 8, 58, 25, "left"),     // Bottom left
            new(3, 25, 15, 35, 28, "left"),    // Top left
            new(4, 12, 50, 28, 50, "center"),  // Top
            new(5, 25, 85, 35, 72, "right"),   // Top right
            new(6, 65, 92, 58, 75, "right")    // Bottom right
        ];
    }

    // 7-max
    private static IReadOnlyList<PlayerLayoutPosition> Get7MaxPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom
            new(2, 75, 10, 64, 25, "left"),    // Bottom left
            new(3, 40, 5, 44, 22, "left"),     // Mid left
            new(4, 12, 30, 28, 38, "center"),  // Top left
            new(5, 12, 70, 28, 62, "center"),  // Top right
            new(6, 40, 95, 44, 78, "right"),   // Mid right
            new(7, 75, 90, 64, 75, "right")    // Bottom right
        ];
    }

    // 8-max
    private static IReadOnlyList<PlayerLayoutPosition> Get8MaxPositions()
    {
        return
        [
            new(1, 88, 50, 72, 50, "center"),  // Bottom
            new(2, 78, 12, 66, 26, "left"),    // Bottom left
            new(3, 50, 5, 50, 22, "left"),     // Left
            new(4, 22, 12, 32, 26, "left"),    // Top left
            new(5, 12, 50, 28, 50, "center"),  // Top
            new(6, 22, 88, 32, 74, "right"),   // Top right
            new(7, 50, 95, 50, 78, "right"),   // Right
            new(8, 78, 88, 66, 74, "right")    // Bottom right
        ];
    }

    // 9-max (standard casino layout)
    private static IReadOnlyList<PlayerLayoutPosition> Get9MaxPositions()
    {
        return
        [
            new(1, 90, 50, 74, 50, "center"),  // Bottom center (hero)
            new(2, 82, 18, 68, 30, "left"),    // Bottom left
            new(3, 55, 5, 52, 22, "left"),     // Mid left
            new(4, 25, 10, 35, 24, "left"),    // Top left
            new(5, 10, 35, 26, 42, "center"),  // Top left center
            new(6, 10, 65, 26, 58, "center"),  // Top right center
            new(7, 25, 90, 35, 76, "right"),   // Top right
            new(8, 55, 95, 52, 78, "right"),   // Mid right
            new(9, 82, 82, 68, 70, "right")    // Bottom right
        ];
    }

    // 10-max
    private static IReadOnlyList<PlayerLayoutPosition> Get10MaxPositions()
    {
        return
        [
            new(1, 90, 50, 74, 50, "center"),  // Bottom center (hero)
            new(2, 85, 22, 70, 32, "left"),    // Bottom left
            new(3, 60, 5, 55, 22, "left"),     // Mid left lower
            new(4, 35, 5, 40, 22, "left"),     // Mid left upper
            new(5, 15, 20, 28, 32, "left"),    // Top left
            new(6, 10, 50, 26, 50, "center"),  // Top center
            new(7, 15, 80, 28, 68, "right"),   // Top right
            new(8, 35, 95, 40, 78, "right"),   // Mid right upper
            new(9, 60, 95, 55, 78, "right"),   // Mid right lower
            new(10, 85, 78, 70, 68, "right")   // Bottom right
        ];
    }

    /// <summary>
    /// Gets CSS variables for responsive scaling based on container size.
    /// </summary>
    public static string GetResponsiveScaleVariables(string breakpoint)
    {
        return breakpoint.ToLowerInvariant() switch
        {
            "mobile" => "--table-scale: 0.6; --card-scale: 0.7; --chip-scale: 0.7;",
            "tablet" => "--table-scale: 0.8; --card-scale: 0.85; --chip-scale: 0.85;",
            "desktop" => "--table-scale: 1.0; --card-scale: 1.0; --chip-scale: 1.0;",
            "large" => "--table-scale: 1.1; --card-scale: 1.1; --chip-scale: 1.1;",
            _ => "--table-scale: 1.0; --card-scale: 1.0; --chip-scale: 1.0;"
        };
    }
}
