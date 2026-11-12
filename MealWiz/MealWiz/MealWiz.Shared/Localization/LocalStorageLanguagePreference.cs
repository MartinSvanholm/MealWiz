using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace MealWiz.Shared.Localization;

/// <summary>
/// The web app's language choice, in local storage. The forced reload restarts the app, and <c>Program.cs</c> resolves
/// the culture from the stored choice again before the first render (the Danish satellite only loads at startup).
/// </summary>
public class LocalStorageLanguagePreference(ILocalStorageService localStorage, NavigationManager navigation) : ILanguagePreference
{
    public async Task<string?> GetAsync()
    {
        var stored = await localStorage.GetItemAsStringAsync(SupportedLanguages.StorageKey);
        return SupportedLanguages.IsSupported(stored) ? stored!.ToLowerInvariant() : null;
    }

    public async Task SetAsync(string? code)
    {
        if (code is not null && !SupportedLanguages.IsSupported(code))
            throw new ArgumentException($"'{code}' is not a supported language.", nameof(code));

        if (code is null)
            await localStorage.RemoveItemAsync(SupportedLanguages.StorageKey);
        else
            // A plain string, not JSON: index.html and Program.cs read the raw value.
            await localStorage.SetItemAsStringAsync(SupportedLanguages.StorageKey, code.ToLowerInvariant());

        navigation.NavigateTo(navigation.Uri, forceLoad: true);
    }
}
