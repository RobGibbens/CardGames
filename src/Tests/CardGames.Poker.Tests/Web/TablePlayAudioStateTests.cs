using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayAudioStateTests
{
    [Fact]
    public async Task InitializeMuteStateAsync_Sets_IsMuted_From_Browser()
    {
        var js = new RecordingJsRuntime { MutedReturnValue = true };
        var state = CreateState(js);

        await state.InitializeMuteStateAsync();

        state.IsMuted.Should().BeTrue();
        js.Calls.Should().ContainSingle(c => c.Identifier == "cardGamesAudio.getMuted");
    }

    [Fact]
    public async Task ToggleMuteAsync_Flips_State_And_Persists_To_Browser()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);

        await state.ToggleMuteAsync();

        state.IsMuted.Should().BeTrue();
        js.Calls.Should().ContainSingle(c =>
            c.Identifier == "cardGamesAudio.setMuted" && (bool)c.Args![0]! == true);

        await state.ToggleMuteAsync();

        state.IsMuted.Should().BeFalse();
    }

    [Fact]
    public async Task PlayDealCardAsync_Skips_When_Muted()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);
        await state.ToggleMuteAsync();
        js.Calls.Clear();

        await state.PlayDealCardAsync(2);

        js.Calls.Should().NotContain(c => c.Identifier == "cardGamesAudio.playDealCard");
    }

    [Fact]
    public async Task PlayDealCardAsync_Skips_When_Count_Not_Positive()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);

        await state.PlayDealCardAsync(0);

        js.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayDealCardAsync_Invokes_Browser_When_Unmuted()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);

        await state.PlayDealCardAsync(3);

        js.Calls.Should().ContainSingle(c =>
            c.Identifier == "cardGamesAudio.playDealCard" && (int)c.Args![0]! == 3);
    }

    [Fact]
    public async Task PlayTurnAlertAsync_Skips_When_Muted()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);
        await state.ToggleMuteAsync();
        js.Calls.Clear();

        await state.PlayTurnAlertAsync();

        js.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task PlayTurnAlertAsync_Plays_Alert_When_Unmuted()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);

        await state.PlayTurnAlertAsync();

        js.Calls.Should().ContainSingle(c =>
            c.Identifier == "cardGamesAudio.playSoundEffect" && (string)c.Args![0]! == "/sounds/alert.mp3");
    }

    [Fact]
    public async Task PlayTableSoundEffectsAsync_Deduplicates_By_CueKey()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);
        var effects = new[]
        {
            CreateEffect("cue-1", "/sounds/win.mp3"),
            CreateEffect("cue-1", "/sounds/win.mp3"),
        };

        await state.PlayTableSoundEffectsAsync(effects);
        await state.PlayTableSoundEffectsAsync(effects);

        js.Calls.Count(c => c.Identifier == "cardGamesAudio.playSoundEffect").Should().Be(1);
    }

    [Fact]
    public async Task ResetPlayedSoundKeys_Allows_Replaying_Same_Cue()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);
        var effects = new[] { CreateEffect("cue-1", "/sounds/win.mp3") };

        await state.PlayTableSoundEffectsAsync(effects);
        state.ResetPlayedSoundKeys();
        await state.PlayTableSoundEffectsAsync(effects);

        js.Calls.Count(c => c.Identifier == "cardGamesAudio.playSoundEffect").Should().Be(2);
    }

    [Fact]
    public async Task PlayTableSoundEffectsAsync_Skips_When_Muted()
    {
        var js = new RecordingJsRuntime();
        var state = CreateState(js);
        await state.ToggleMuteAsync();
        js.Calls.Clear();

        await state.PlayTableSoundEffectsAsync(new[] { CreateEffect("cue-1", "/sounds/win.mp3") });

        js.Calls.Should().BeEmpty();
    }

    private static TablePlayAudioState CreateState(IJSRuntime js)
        => new(js, NullLogger<TablePlayAudioState>.Instance);

    private static TableSoundEffectDto CreateEffect(string cueKey, string source) => new()
    {
        CueKey = cueKey,
        EventKey = "winning",
        HandNumber = 1,
        Source = source,
    };

    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public List<(string Identifier, object?[]? Args)> Calls { get; } = [];

        public bool MutedReturnValue { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add((identifier, args));

            if (identifier == "cardGamesAudio.getMuted" && typeof(TValue) == typeof(bool))
            {
                return new ValueTask<TValue>((TValue)(object)MutedReturnValue);
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
