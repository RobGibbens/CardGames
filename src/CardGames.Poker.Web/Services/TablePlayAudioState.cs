using CardGames.Contracts.SignalR;
using Microsoft.JSInterop;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Circuit-scoped state service that owns the table page's audio playback: the persisted
/// deal-sound mute state, per-hand sound-effect de-duplication, and the JS interop calls that
/// actually play sounds. The table component delegates all audio concerns here so that the
/// page code-behind stays focused on gameplay orchestration.
/// Modelled after <see cref="TablePlayToastState"/>.
/// </summary>
public sealed class TablePlayAudioState(IJSRuntime jsRuntime, ILogger<TablePlayAudioState> logger)
{
    private readonly HashSet<string> _playedSoundKeys = [];

    /// <summary>
    /// Whether table sound effects are currently muted. Persisted client-side and refreshed via
    /// <see cref="InitializeMuteStateAsync"/>; toggled via <see cref="ToggleMuteAsync"/>.
    /// </summary>
    public bool IsMuted { get; private set; }

    /// <summary>
    /// Loads the persisted mute state from the browser. Any interop failure is allowed to
    /// propagate so the caller can decide whether JS interop is ready yet.
    /// </summary>
    public async Task InitializeMuteStateAsync()
    {
        IsMuted = await jsRuntime.InvokeAsync<bool>("cardGamesAudio.getMuted");
    }

    /// <summary>
    /// Flips the mute state and persists it to the browser.
    /// </summary>
    public async Task ToggleMuteAsync()
    {
        IsMuted = !IsMuted;

        try
        {
            await jsRuntime.InvokeVoidAsync("cardGamesAudio.setMuted", IsMuted);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to toggle deal-card sound mute state");
        }
    }

    /// <summary>
    /// Plays the "it's your turn" alert sound, unless muted.
    /// </summary>
    public async Task PlayTurnAlertAsync()
    {
        if (IsMuted)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("cardGamesAudio.playSoundEffect", "/sounds/alert.mp3");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to play turn alert sound");
        }
    }

    /// <summary>
    /// Plays the card-dealing sound for the given number of cards, unless muted.
    /// </summary>
    /// <param name="cardCount">How many cards were dealt; ignored when not positive.</param>
    public async Task PlayDealCardAsync(int cardCount)
    {
        if (cardCount <= 0 || IsMuted)
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("cardGamesAudio.playDealCard", cardCount);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Unable to play deal-card sound effect");
        }
    }

    /// <summary>
    /// Plays each table sound effect that has not already been played this hand, unless muted.
    /// </summary>
    public async Task PlayTableSoundEffectsAsync(IReadOnlyList<TableSoundEffectDto>? soundEffects)
    {
        if (soundEffects is null || soundEffects.Count == 0 || IsMuted)
        {
            return;
        }

        foreach (var soundEffect in soundEffects)
        {
            await PlayTableSoundEffectAsync(soundEffect);
        }
    }

    /// <summary>
    /// Clears the per-hand de-duplication set so the same cue keys can play again in a new hand.
    /// </summary>
    public void ResetPlayedSoundKeys() => _playedSoundKeys.Clear();

    private async Task PlayTableSoundEffectAsync(TableSoundEffectDto soundEffect)
    {
        if (string.IsNullOrWhiteSpace(soundEffect.CueKey) || string.IsNullOrWhiteSpace(soundEffect.Source))
        {
            return;
        }

        if (!_playedSoundKeys.Add(soundEffect.CueKey))
        {
            return;
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("cardGamesAudio.playSoundEffect", soundEffect.Source);
        }
        catch (Exception ex)
        {
            _playedSoundKeys.Remove(soundEffect.CueKey);
            logger.LogDebug(ex, "Unable to play {SoundEventKey} sound effect", soundEffect.EventKey);
        }
    }
}
