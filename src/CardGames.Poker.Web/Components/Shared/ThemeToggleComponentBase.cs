using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CardGames.Poker.Web.Components.Shared;

public abstract class ThemeToggleComponentBase : ComponentBase, IAsyncDisposable
{
    private const string ThemeModulePath = "/themePreference.js";

    private IJSObjectReference? _themeModule;

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    protected ThemePreference ThemePreference { get; private set; } = ThemePreference.System;

    protected string ThemeIconClass => ThemePreference switch
    {
        ThemePreference.Light => "fa-solid fa-sun",
        ThemePreference.Dark => "fa-solid fa-moon",
        _ => "fa-solid fa-desktop"
    };

    protected string ThemeToggleTitle => $"Theme: {ThemePreference.ToDisplayName()}";

    protected string ThemeToggleAriaLabel => $"Theme: {ThemePreference.ToDisplayName()}. Click to switch to {ThemePreference.Next().ToDisplayName()}.";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            _themeModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", ThemeModulePath);
            var storedPreference = await _themeModule.InvokeAsync<string?>("getStoredThemePreference");
            ThemePreference = ThemePreferenceExtensions.FromStorageValue(storedPreference);

            await ApplyThemePreferenceAsync();
            StateHasChanged();
        }
        catch (InvalidOperationException)
        {
            // Ignore JS interop errors during SSR.
        }
        catch (JSDisconnectedException)
        {
            // Ignore circuit disconnects during teardown.
        }
        catch (JSException)
        {
            // Ignore JS module load failures and keep the default theme.
        }
    }

    protected async Task ToggleThemeAsync()
    {
        ThemePreference = ThemePreference.Next();
        await ApplyThemePreferenceAsync();
        StateHasChanged();
    }

    private async Task ApplyThemePreferenceAsync()
    {
        if (_themeModule is null)
        {
            return;
        }

        await _themeModule.InvokeVoidAsync("applyThemePreference", ThemePreference.ToStorageValue());
    }

    public async ValueTask DisposeAsync()
    {
        if (_themeModule is not null)
        {
            try
            {
                await _themeModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Ignore circuit disconnects during teardown.
            }
        }
    }
}