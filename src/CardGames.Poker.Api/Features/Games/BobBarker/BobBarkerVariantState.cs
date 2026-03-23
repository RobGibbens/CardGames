using System.Text.Json;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.BobBarker;

internal static class BobBarkerVariantState
{
    public static int? GetSelectedShowcaseDealOrder(GamePlayer player)
    {
        if (string.IsNullOrWhiteSpace(player.VariantState))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<State>(player.VariantState);
            return state?.SelectedShowcaseDealOrder;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void SetSelectedShowcaseDealOrder(GamePlayer player, int dealOrder)
    {
        player.VariantState = JsonSerializer.Serialize(new State
        {
            SelectedShowcaseDealOrder = dealOrder
        });
    }

    private sealed record State
    {
        public int SelectedShowcaseDealOrder { get; init; }
    }
}