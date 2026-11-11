using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Localization;

public static class StringLocalizerExtensions
{
    /// <summary>
    /// A count with its noun, from two keys: <c>&lt;key&gt;_One</c> for exactly 1 and <c>&lt;key&gt;_Other</c> for
    /// everything else (English and Danish need no more forms). Both values carry <c>{0}</c> for the count, e.g.
    /// <c>Items_One</c> = "{0} item", <c>Items_Other</c> = "{0} items". Never glue a word onto a number in code.
    /// </summary>
    public static string Plural(this IStringLocalizer localizer, string key, int count) =>
        localizer[count == 1 ? $"{key}_One" : $"{key}_Other", count].Value;
}
