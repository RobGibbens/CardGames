using Microsoft.JSInterop;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Sound effect types for poker game actions.
/// </summary>
public enum SoundEffect
{
    // Card sounds
    CardDeal,
    CardFlip,
    CardShuffle,
    
    // Chip sounds
    ChipsBet,
    ChipsCollect,
    ChipsWin,
    
    // Action sounds
    Check,
    Call,
    Raise,
    Fold,
    AllIn,
    
    // Game sounds
    YourTurn,
    TimerWarning,
    TimerCritical,
    HandWin,
    HandLose,
    NewHand,
    
    // UI sounds
    ButtonClick,
    NotificationPop,
    Error
}

/// <summary>
/// Settings for sound effects.
/// </summary>
public record SoundSettings(
    bool Enabled = true,
    int MasterVolume = 75,
    bool ActionSounds = true,
    bool TurnNotifications = true,
    bool WinSounds = true,
    bool CardSounds = true,
    bool ChipSounds = true);

/// <summary>
/// Service for playing sound effects in the poker game.
/// Uses JavaScript interop to play audio.
/// </summary>
public class SoundEffectsService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private SoundSettings _settings = new();
    private IJSObjectReference? _soundModule;
    private bool _isInitialized;

    public SoundEffectsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Initializes the sound effects system.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _soundModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "eval",
                GetSoundModuleScript());
            _isInitialized = true;
        }
        catch
        {
            // Sound initialization failed - continue silently
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Updates sound settings.
    /// </summary>
    public void UpdateSettings(SoundSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Gets the current sound settings.
    /// </summary>
    public SoundSettings GetSettings() => _settings;

    /// <summary>
    /// Plays a sound effect.
    /// </summary>
    public async Task PlayAsync(SoundEffect effect)
    {
        if (!_settings.Enabled) return;
        if (!ShouldPlaySound(effect)) return;

        try
        {
            var frequency = GetFrequency(effect);
            var duration = GetDuration(effect);
            var volume = CalculateVolume(effect);

            await _jsRuntime.InvokeVoidAsync(
                "eval",
                GetPlayToneScript(frequency, duration, volume));
        }
        catch
        {
            // Sound playback failed - continue silently
        }
    }

    /// <summary>
    /// Plays a card dealing sound.
    /// </summary>
    public Task PlayCardDealAsync() => PlayAsync(SoundEffect.CardDeal);

    /// <summary>
    /// Plays a chip betting sound.
    /// </summary>
    public Task PlayChipsBetAsync() => PlayAsync(SoundEffect.ChipsBet);

    /// <summary>
    /// Plays a check sound.
    /// </summary>
    public Task PlayCheckAsync() => PlayAsync(SoundEffect.Check);

    /// <summary>
    /// Plays a fold sound.
    /// </summary>
    public Task PlayFoldAsync() => PlayAsync(SoundEffect.Fold);

    /// <summary>
    /// Plays a call sound.
    /// </summary>
    public Task PlayCallAsync() => PlayAsync(SoundEffect.Call);

    /// <summary>
    /// Plays a raise sound.
    /// </summary>
    public Task PlayRaiseAsync() => PlayAsync(SoundEffect.Raise);

    /// <summary>
    /// Plays an all-in sound.
    /// </summary>
    public Task PlayAllInAsync() => PlayAsync(SoundEffect.AllIn);

    /// <summary>
    /// Plays a "your turn" notification.
    /// </summary>
    public Task PlayYourTurnAsync() => PlayAsync(SoundEffect.YourTurn);

    /// <summary>
    /// Plays a timer warning sound.
    /// </summary>
    public Task PlayTimerWarningAsync() => PlayAsync(SoundEffect.TimerWarning);

    /// <summary>
    /// Plays a hand win sound.
    /// </summary>
    public Task PlayHandWinAsync() => PlayAsync(SoundEffect.HandWin);

    /// <summary>
    /// Plays a hand lose sound.
    /// </summary>
    public Task PlayHandLoseAsync() => PlayAsync(SoundEffect.HandLose);

    private bool ShouldPlaySound(SoundEffect effect)
    {
        return effect switch
        {
            SoundEffect.CardDeal or SoundEffect.CardFlip or SoundEffect.CardShuffle
                => _settings.CardSounds,
            
            SoundEffect.ChipsBet or SoundEffect.ChipsCollect or SoundEffect.ChipsWin
                => _settings.ChipSounds,
            
            SoundEffect.Check or SoundEffect.Call or SoundEffect.Raise or 
            SoundEffect.Fold or SoundEffect.AllIn
                => _settings.ActionSounds,
            
            SoundEffect.YourTurn or SoundEffect.TimerWarning or SoundEffect.TimerCritical
                => _settings.TurnNotifications,
            
            SoundEffect.HandWin or SoundEffect.HandLose
                => _settings.WinSounds,
            
            _ => true
        };
    }

    private double CalculateVolume(SoundEffect effect)
    {
        var baseVolume = _settings.MasterVolume / 100.0;
        
        // Adjust volume based on effect type
        return effect switch
        {
            SoundEffect.YourTurn or SoundEffect.TimerCritical => baseVolume * 1.2,
            SoundEffect.HandWin => baseVolume * 1.1,
            SoundEffect.ButtonClick => baseVolume * 0.5,
            _ => baseVolume
        };
    }

    private static double GetFrequency(SoundEffect effect)
    {
        return effect switch
        {
            // Card sounds - short percussive
            SoundEffect.CardDeal => 800,
            SoundEffect.CardFlip => 600,
            SoundEffect.CardShuffle => 500,
            
            // Chip sounds - clinking
            SoundEffect.ChipsBet => 1200,
            SoundEffect.ChipsCollect => 1400,
            SoundEffect.ChipsWin => 1600,
            
            // Action sounds
            SoundEffect.Check => 440,
            SoundEffect.Call => 523,
            SoundEffect.Raise => 659,
            SoundEffect.Fold => 330,
            SoundEffect.AllIn => 880,
            
            // Notification sounds
            SoundEffect.YourTurn => 1047,
            SoundEffect.TimerWarning => 800,
            SoundEffect.TimerCritical => 1200,
            
            // Win/lose sounds
            SoundEffect.HandWin => 1319,
            SoundEffect.HandLose => 262,
            SoundEffect.NewHand => 523,
            
            // UI sounds
            SoundEffect.ButtonClick => 600,
            SoundEffect.NotificationPop => 1000,
            SoundEffect.Error => 300,
            
            _ => 440
        };
    }

    private static int GetDuration(SoundEffect effect)
    {
        return effect switch
        {
            SoundEffect.CardDeal => 50,
            SoundEffect.CardFlip => 40,
            SoundEffect.CardShuffle => 200,
            SoundEffect.ChipsBet => 80,
            SoundEffect.ChipsCollect => 100,
            SoundEffect.ChipsWin => 300,
            SoundEffect.Check => 60,
            SoundEffect.Call => 80,
            SoundEffect.Raise => 100,
            SoundEffect.Fold => 70,
            SoundEffect.AllIn => 200,
            SoundEffect.YourTurn => 150,
            SoundEffect.TimerWarning => 100,
            SoundEffect.TimerCritical => 80,
            SoundEffect.HandWin => 400,
            SoundEffect.HandLose => 200,
            SoundEffect.NewHand => 100,
            SoundEffect.ButtonClick => 30,
            SoundEffect.NotificationPop => 80,
            SoundEffect.Error => 150,
            _ => 100
        };
    }

    private static string GetSoundModuleScript()
    {
        return @"
            (function() {
                return {
                    audioContext: null,
                    getContext: function() {
                        if (!this.audioContext) {
                            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
                        }
                        return this.audioContext;
                    }
                };
            })()
        ";
    }

    private static string GetPlayToneScript(double frequency, int durationMs, double volume)
    {
        return $@"
            (function() {{
                try {{
                    const ctx = new (window.AudioContext || window.webkitAudioContext)();
                    const oscillator = ctx.createOscillator();
                    const gainNode = ctx.createGain();
                    
                    oscillator.connect(gainNode);
                    gainNode.connect(ctx.destination);
                    
                    oscillator.frequency.value = {frequency};
                    oscillator.type = 'sine';
                    
                    const volume = Math.min(1, Math.max(0, {volume}));
                    gainNode.gain.setValueAtTime(volume * 0.3, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + {durationMs / 1000.0});
                    
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + {durationMs / 1000.0});
                }} catch(e) {{
                    // Silently fail if audio context not available
                }}
            }})()
        ";
    }

    public async ValueTask DisposeAsync()
    {
        if (_soundModule != null)
        {
            try
            {
                await _soundModule.DisposeAsync();
            }
            catch
            {
                // Disposal failed - continue silently
            }
        }
    }
}
