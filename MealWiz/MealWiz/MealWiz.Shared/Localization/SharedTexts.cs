using System.Globalization;
using System.Resources;

namespace MealWiz.Shared.Localization;

/// <summary>
/// The shared resource for code that can't inject <c>IStringLocalizer&lt;SharedResource&gt;</c> (static helpers such
/// as <c>ErrorMessages</c>, <c>ResultHelper</c> and <see cref="DisplayDates"/>). Reads
/// <see cref="CultureInfo.CurrentUICulture"/> on every call.
/// </summary>
public static class SharedTexts
{
    private static readonly ResourceManager Resources = new(typeof(SharedResource).FullName!, typeof(SharedResource).Assembly);

    /// <summary>The text for <paramref name="key"/> in the current UI culture, or the key itself if there's none.</summary>
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
