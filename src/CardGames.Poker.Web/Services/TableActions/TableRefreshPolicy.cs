namespace CardGames.Poker.Web.Services.TableActions;

/// <summary>
/// A logical interaction on the table page that may require server state to be
/// re-read or re-rendered. Interactions are grouped by family rather than by the
/// individual button/handler so the refresh decision stays consistent across the
/// page.
/// </summary>
/// <remarks>
/// This enum is the vocabulary used by <see cref="TableRefreshPolicy"/>. The page
/// (<c>TablePlay.razor</c>) maps each handler to one of these interactions and asks
/// the policy what to do, instead of repeating ad-hoc refresh decisions inline.
/// </remarks>
public enum TableInteraction
{
    /// <summary>First render / route load. The server snapshot is the only source.</summary>
    InitialLoad,

    /// <summary>The host approved this player's join request and the player is now seated.</summary>
    JoinRequestApproved,

    /// <summary>
    /// Manual game start could not be re-synced over the hub (no live connection) and
    /// the page must fall back to an HTTP reload. The hub snapshot is the preferred path.
    /// </summary>
    ManualStartFallback,

    /// <summary>A betting action (bet, call, raise, check, fold) succeeded.</summary>
    BettingAction,

    /// <summary>A draw / discard action succeeded.</summary>
    DrawDiscardAction,

    /// <summary>A special-variant decision succeeded (drop-or-stay, keep-or-trade, buy-card, pot-match, etc.).</summary>
    SpecialVariantDecision,

    /// <summary>Chips were added to the current player's stack via the cashier.</summary>
    AddChips,

    /// <summary>The current player toggled sit-out / sit-in.</summary>
    SitOutToggle,

    /// <summary>The current player left (or requested to leave) the table.</summary>
    LeaveTable,

    /// <summary>The host toggled odds visibility; the authoritative value is on the action response.</summary>
    OddsVisibilityToggle,

    /// <summary>A table-settings update arrived over the hub (e.g. odds visibility changed by the host).</summary>
    TableSettingsPush,
}

/// <summary>
/// How the table page should react to a <see cref="TableInteraction"/>.
/// These are the four refresh strategies the page is allowed to use; any new
/// interaction must map to exactly one of them.
/// </summary>
public enum TableRefreshKind
{
    /// <summary>
    /// Re-read the full table snapshot from the server (game + players + rules).
    /// Expensive — reserved for bootstrap and fallback cases where multiple slices
    /// changed and no authoritative hub push is guaranteed.
    /// </summary>
    Full,

    /// <summary>
    /// Re-read a single focused slice (e.g. odds visibility / settings) rather than
    /// the whole table. Cheaper than <see cref="Full"/> and used when only one part changed.
    /// </summary>
    Slice,

    /// <summary>
    /// Do nothing now: the server already broadcasts the authoritative update over a hub,
    /// so an immediate refetch would only duplicate the incoming push.
    /// </summary>
    HubDriven,

    /// <summary>
    /// Apply a local-only UI transition. Used when the change is purely presentational,
    /// already applied from the action response, or the page is navigating away.
    /// </summary>
    LocalOnly,
}

/// <summary>
/// The single source of truth for how the table page refreshes server state.
/// </summary>
/// <remarks>
/// <para>
/// The table page coordinates direct API calls and hub-driven updates. Without a shared
/// policy these two paths tend to fight (an action does a full reload <em>and</em> a hub
/// event triggers another), which is wasteful because the backend table-state read is
/// non-trivial. Centralizing the decision here keeps post-action behavior consistent and
/// makes the policy auditable and testable.
/// </para>
/// <para>The full prose policy lives in <c>docs/TableRefreshPolicy.md</c>.</para>
/// </remarks>
public static class TableRefreshPolicy
{
    /// <summary>
    /// Resolves which refresh strategy the table page should apply for a given interaction.
    /// </summary>
    public static TableRefreshKind ResolveRefreshKind(TableInteraction interaction) => interaction switch
    {
        // Bootstrap / fallback: the server snapshot is the only authoritative source and a
        // prompt hub push is not guaranteed, so re-read everything.
        TableInteraction.InitialLoad => TableRefreshKind.Full,
        TableInteraction.JoinRequestApproved => TableRefreshKind.Full,
        TableInteraction.ManualStartFallback => TableRefreshKind.Full,

        // Gameplay actions: the API mutates server state and the hub promptly broadcasts the
        // updated public/private table state, so the page waits for that push instead of
        // immediately refetching (which would duplicate the broadcast).
        TableInteraction.BettingAction => TableRefreshKind.HubDriven,
        TableInteraction.DrawDiscardAction => TableRefreshKind.HubDriven,
        TableInteraction.SpecialVariantDecision => TableRefreshKind.HubDriven,
        TableInteraction.AddChips => TableRefreshKind.HubDriven,
        TableInteraction.SitOutToggle => TableRefreshKind.HubDriven,

        // Focused slice: only the settings/odds slice changed; re-read just that part.
        TableInteraction.TableSettingsPush => TableRefreshKind.Slice,

        // Purely local transitions: the authoritative value is already on the action response,
        // or the page is navigating away from the table.
        TableInteraction.OddsVisibilityToggle => TableRefreshKind.LocalOnly,
        TableInteraction.LeaveTable => TableRefreshKind.LocalOnly,

        _ => TableRefreshKind.HubDriven,
    };
}
