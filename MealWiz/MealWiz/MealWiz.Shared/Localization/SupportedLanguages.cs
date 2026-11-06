namespace MealWiz.Shared.Localization;

/// <summary>A language the app is translated into: its two-letter code and its name in that language.</summary>
public sealed record SupportedLanguage(string Code, string NativeName);

/// <summary>
/// The one list of languages the app supports, read by <see cref="CultureResolver"/> and the Account page's language
/// dropdown. Adding a language: one entry here plus a <c>.xx.resx</c> beside every resource.
/// </summary>
public static class SupportedLanguages
{
    /// <summary>
    /// Where the user's choice is kept on the device: local storage on the web, MAUI <c>Preferences</c> in the app.
    /// The value is a bare code from <see cref="All"/> (<c>da</c>), stored as a plain string, not JSON (write it with
    /// <c>SetItemAsStringAsync</c>). No key means "Device default". Both index.html files read it too.
    /// </summary>
    public const string StorageKey = "mealwiz.language";

    /// <summary>The fallback language, and the one the neutral .resx files hold.</summary>
    public const string DefaultCode = "en";

    public static IReadOnlyList<SupportedLanguage> All { get; } =
    [
        new("en", "English"),
        new("da", "Dansk"),
    ];

    /// <summary>Whether <paramref name="code"/> is one of <see cref="All"/>'s codes (case-insensitive).</summary>
    public static bool IsSupported(string? code) =>
        code is not null && All.Any(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
}
