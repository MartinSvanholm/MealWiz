using System.Globalization;

namespace MealWiz.Shared.Localization;

/// <summary>Picks the app's language once at startup, before the first render (both hosts call it).</summary>
public static class CultureResolver
{
    /// <summary>
    /// The culture to run in: the stored choice if it's a supported code, otherwise the first supported language in
    /// the device culture or its parents (any <c>da-*</c> gives <c>da</c>), otherwise English. Always a neutral culture.
    /// </summary>
    public static CultureInfo Resolve(string? storedChoice, CultureInfo deviceCulture)
    {
        if (SupportedLanguages.IsSupported(storedChoice))
            return CultureInfo.GetCultureInfo(storedChoice!.ToLowerInvariant());

        for (var culture = deviceCulture; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            if (SupportedLanguages.IsSupported(culture.TwoLetterISOLanguageName))
                return CultureInfo.GetCultureInfo(culture.TwoLetterISOLanguageName);
        }

        return CultureInfo.GetCultureInfo(SupportedLanguages.DefaultCode);
    }

    /// <summary>
    /// Makes <paramref name="culture"/> the app's culture: the defaults for every thread (Blazor WebAssembly loads
    /// satellite assemblies by them in RunAsync) and the current thread's.
    /// </summary>
    public static void Apply(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
