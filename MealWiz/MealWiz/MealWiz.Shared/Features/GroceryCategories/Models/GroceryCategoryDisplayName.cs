using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Features.GroceryCategories.Models;

public static class GroceryCategoryDisplayName
{
    /// <summary>
    /// The name to show for <paramref name="category"/>: the user's own text when set, otherwise a default category's
    /// name in the UI language. An emoji, when set, goes before the name.
    /// </summary>
    public static string DisplayName(this GroceryCategory category, IStringLocalizer<GroceryCategoriesResource> localizer)
    {
        var name = !string.IsNullOrWhiteSpace(category.Name)
            ? category.Name
            : category.DefaultKey is { } key ? localizer[ResourceKey(key)].Value : string.Empty;

        return string.IsNullOrWhiteSpace(category.Emoji) ? name : $"{category.Emoji} {name}";
    }

    /// <summary>The resource key of a default category's name, e.g. <c>DefaultCategory_DairyEggs_Name</c>.</summary>
    public static string ResourceKey(DefaultCategoryKey key) => $"DefaultCategory_{key}_Name";
}
