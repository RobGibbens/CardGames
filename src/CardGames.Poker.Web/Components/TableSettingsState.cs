namespace CardGames.Poker.Web.Components;

/// <summary>
/// Represents the current state of table settings.
/// </summary>
public record TableSettingsState(
    string Theme,
    bool ShowAvatars,
    bool ShowDealerButton,
    bool ShowAnimations,
    bool CompactMode,
    bool ShowTimeBank,
    bool AutoFoldOnTimeout,
    bool SoundEnabled,
    int MasterVolume,
    bool ActionSounds,
    bool TurnNotifications,
    bool WinSounds,
    string SelectedCardBack);
