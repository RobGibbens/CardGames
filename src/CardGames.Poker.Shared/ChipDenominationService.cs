using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Shared;

/// <summary>
/// Service for converting chip amounts to visual chip stack representations.
/// </summary>
public static class ChipDenominationService
{
    /// <summary>
    /// Standard casino chip denominations with their associated colors.
    /// </summary>
    public static readonly IReadOnlyList<(int Value, string Color)> StandardDenominations =
    [
        (1, "#FFFFFF"),       // White - $1
        (5, "#FF0000"),       // Red - $5
        (10, "#0000FF"),      // Blue - $10
        (25, "#00FF00"),      // Green - $25
        (50, "#FFA500"),      // Orange - $50
        (100, "#000000"),     // Black - $100
        (500, "#800080"),     // Purple - $500
        (1000, "#FFFF00"),    // Yellow - $1,000
        (5000, "#FF69B4"),    // Pink - $5,000
        (10000, "#D4AF37"),   // Gold - $10,000
        (25000, "#C0C0C0"),   // Silver - $25,000
        (100000, "#964B00")   // Brown - $100,000
    ];

    /// <summary>
    /// Converts a chip amount to a visual stack representation using standard denominations.
    /// Uses a greedy algorithm to minimize the number of chips.
    /// </summary>
    /// <param name="amount">The total chip amount to convert.</param>
    /// <returns>A ChipStackDto with the breakdown by denomination.</returns>
    public static ChipStackDto ConvertToChipStack(int amount)
    {
        return ConvertToChipStack(amount, StandardDenominations);
    }

    /// <summary>
    /// Converts a chip amount to a visual stack representation using custom denominations.
    /// </summary>
    /// <param name="amount">The total chip amount to convert.</param>
    /// <param name="denominations">The denominations to use, ordered from smallest to largest value.</param>
    /// <returns>A ChipStackDto with the breakdown by denomination.</returns>
    public static ChipStackDto ConvertToChipStack(int amount, IReadOnlyList<(int Value, string Color)> denominations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (amount == 0)
        {
            return new ChipStackDto(0, []);
        }

        var chips = new List<ChipDto>();
        var remaining = amount;

        // Process denominations from largest to smallest
        for (var i = denominations.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var (value, color) = denominations[i];
            if (value <= remaining)
            {
                var count = remaining / value;
                if (count > 0)
                {
                    chips.Add(new ChipDto(value, color, count));
                    remaining -= count * value;
                }
            }
        }

        return new ChipStackDto(amount, chips);
    }

    /// <summary>
    /// Gets the color for a specific chip denomination.
    /// </summary>
    /// <param name="denomination">The chip denomination value.</param>
    /// <returns>The color string for the denomination, or a default color if not found.</returns>
    public static string GetColorForDenomination(int denomination)
    {
        foreach (var (value, color) in StandardDenominations)
        {
            if (value == denomination)
            {
                return color;
            }
        }
        return "#808080"; // Gray as default
    }

    /// <summary>
    /// Gets the denomination name for display purposes.
    /// </summary>
    /// <param name="denomination">The chip denomination value.</param>
    /// <returns>A formatted string representing the denomination (e.g., "$100", "$1K", "$10K").</returns>
    public static string GetDenominationLabel(int denomination)
    {
        return denomination switch
        {
            >= 1000000 => $"${denomination / 1000000}M",
            >= 1000 => $"${denomination / 1000}K",
            _ => $"${denomination}"
        };
    }

    /// <summary>
    /// Creates a simplified chip stack for display, limiting the number of different denominations shown.
    /// </summary>
    /// <param name="amount">The total chip amount.</param>
    /// <param name="maxDenominations">Maximum number of different denominations to display.</param>
    /// <returns>A ChipStackDto with limited denominations for cleaner visual display.</returns>
    public static ChipStackDto ConvertToSimplifiedChipStack(int amount, int maxDenominations = 4)
    {
        var fullStack = ConvertToChipStack(amount);
        
        if (fullStack.Chips.Count <= maxDenominations)
        {
            return fullStack;
        }

        // Take the largest denominations and combine the rest
        var simplifiedChips = fullStack.Chips
            .Take(maxDenominations)
            .ToList();

        return new ChipStackDto(amount, simplifiedChips);
    }

    /// <summary>
    /// Gets the chip stack for a blind or ante amount, using appropriate small denominations.
    /// </summary>
    /// <param name="blindAmount">The blind or ante amount.</param>
    /// <returns>A ChipStackDto optimized for blind display.</returns>
    public static ChipStackDto ConvertBlindToChipStack(int blindAmount)
    {
        // For blinds, we might want to use specific denominations based on the blind level
        return ConvertToChipStack(blindAmount);
    }
}
