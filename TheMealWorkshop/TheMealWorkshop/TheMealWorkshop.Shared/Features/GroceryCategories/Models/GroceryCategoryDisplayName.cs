using Microsoft.Extensions.Localization;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.Models;

public static class GroceryCategoryDisplayName
{
    /// <summary>
    /// The name to show for <paramref name="category"/>: the user's own text when set, otherwise a default category's
    /// name in the UI language. An emoji, when set, goes before the name.
    /// </summary>
    public static string DisplayName(this GroceryCategory category, IStringLocalizer<GroceryCategoriesResource> localizer)
    {
        var name = category.DisplayNameWithoutEmoji(localizer);
        return string.IsNullOrWhiteSpace(category.Emoji) ? name : $"{category.Emoji} {name}";
    }

    /// <summary>The user's own text when set, otherwise a default category's name in the UI language.</summary>
    public static string DisplayNameWithoutEmoji(this GroceryCategory category, IStringLocalizer<GroceryCategoriesResource> localizer) =>
        !string.IsNullOrWhiteSpace(category.Name)
            ? category.Name
            : category.DefaultKey is { } key ? localizer[ResourceKey(key)].Value : string.Empty;

    /// <summary>The resource key of a default category's name, e.g. <c>DefaultCategory_DairyEggs_Name</c>.</summary>
    public static string ResourceKey(DefaultCategoryKey key) => $"DefaultCategory_{key}_Name";
}
