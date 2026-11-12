using System.Globalization;
using MealWiz.Shared.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Storage;

namespace MealWiz.Providers;

/// <summary>
/// The app's language choice, in MAUI <c>Preferences</c>. A reload in the BlazorWebView doesn't re-run
/// <c>MauiProgram</c>, so the switch applies the culture in-process first, then reloads so every component renders in
/// a fresh scope. <paramref name="deviceCulture"/> is the OS culture captured at startup, before it was overridden.
/// </summary>
public class PreferencesLanguagePreference(NavigationManager navigation, CultureInfo deviceCulture) : ILanguagePreference
{
    public Task<string?> GetAsync()
    {
        var stored = Preferences.Default.Get<string?>(SupportedLanguages.StorageKey, null);
        return Task.FromResult(SupportedLanguages.IsSupported(stored) ? stored!.ToLowerInvariant() : null);
    }

    public Task SetAsync(string? code)
    {
        if (code is not null && !SupportedLanguages.IsSupported(code))
            throw new ArgumentException($"'{code}' is not a supported language.", nameof(code));

        if (code is null)
            Preferences.Default.Remove(SupportedLanguages.StorageKey);
        else
            Preferences.Default.Set(SupportedLanguages.StorageKey, code.ToLowerInvariant());

        CultureResolver.Apply(CultureResolver.Resolve(code, deviceCulture));
        navigation.NavigateTo(navigation.Uri, forceLoad: true);
        return Task.CompletedTask;
    }
}
