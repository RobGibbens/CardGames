namespace CardGames.Poker.Web.Services;

/// <summary>
/// Scoped service for managing the Dashboard panel UI state.
/// Handles panel open/close and section expand/collapse states.
/// </summary>
public sealed class DashboardState
{
    private bool _isOpen;
    private readonly Dictionary<string, bool> _sectionStates = new()
    {
        ["Leaderboard"] = true,
        ["Odds"] = true,
        ["HandHistory"] = true
    };

    /// <summary>
    /// Gets whether the dashboard panel is currently open.
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// Fired when any dashboard state changes.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Opens the dashboard panel.
    /// </summary>
    public void Open()
    {
        if (!_isOpen)
        {
            _isOpen = true;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Closes the dashboard panel.
    /// </summary>
    public void Close()
    {
        if (_isOpen)
        {
            _isOpen = false;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Toggles the dashboard panel open/closed state.
    /// </summary>
    public void Toggle()
    {
        _isOpen = !_isOpen;
        NotifyStateChanged();
    }

    /// <summary>
    /// Gets whether a specific section is expanded.
    /// </summary>
    /// <param name="sectionId">The section identifier (Leaderboard, Odds, HandHistory).</param>
    /// <returns>True if expanded, false if collapsed.</returns>
    public bool IsSectionExpanded(string sectionId)
    {
        return _sectionStates.TryGetValue(sectionId, out var isExpanded) && isExpanded;
    }

    /// <summary>
    /// Sets the expanded state for a specific section.
    /// </summary>
    /// <param name="sectionId">The section identifier.</param>
    /// <param name="isExpanded">Whether the section should be expanded.</param>
    public void SetSectionExpanded(string sectionId, bool isExpanded)
    {
        if (_sectionStates.TryGetValue(sectionId, out var current) && current == isExpanded)
        {
            return;
        }

        _sectionStates[sectionId] = isExpanded;
        NotifyStateChanged();
    }

    /// <summary>
    /// Toggles the expanded state for a specific section.
    /// </summary>
    /// <param name="sectionId">The section identifier.</param>
    public void ToggleSection(string sectionId)
    {
        _sectionStates[sectionId] = !IsSectionExpanded(sectionId);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
