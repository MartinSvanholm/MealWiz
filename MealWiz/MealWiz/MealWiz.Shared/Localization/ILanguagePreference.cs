namespace MealWiz.Shared.Localization;

/// <summary>
/// The user's language choice on this device (the Account page's dropdown). Each host has its own store: local
/// storage on the web (<see cref="LocalStorageLanguagePreference"/>), MAUI <c>Preferences</c> in the app. Both use
/// <see cref="SupportedLanguages.StorageKey"/> and a bare code as a plain string.
/// </summary>
public interface ILanguagePreference
{
    /// <summary>The stored code from <see cref="SupportedLanguages.All"/>, or null for "Device default".</summary>
    Task<string?> GetAsync();

    /// <summary>
    /// Stores <paramref name="code"/> (null removes the choice, so the device language applies again) and reloads the
    /// current page in that language. Throws <see cref="ArgumentException"/> for an unsupported code.
    /// </summary>
    Task SetAsync(string? code);
}
