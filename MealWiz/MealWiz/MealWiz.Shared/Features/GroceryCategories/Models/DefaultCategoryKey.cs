namespace MealWiz.Shared.Features.GroceryCategories.Models;

/// <summary>
/// The eleven default categories, in their default supermarket order. Stored as text in
/// <c>grocery_categories.default_key</c>; the dictionary maps words to these keys.
/// </summary>
public enum DefaultCategoryKey
{
    Produce,
    Bakery,
    MeatFish,
    Deli,
    DairyEggs,
    Frozen,
    Pantry,
    Drinks,
    Snacks,
    Household,
    Other,
}

public static class DefaultCategoryKeyExtensions
{
    private static readonly IReadOnlyDictionary<DefaultCategoryKey, string> DbValues = new Dictionary<DefaultCategoryKey, string>
    {
        [DefaultCategoryKey.Produce] = "produce",
        [DefaultCategoryKey.Bakery] = "bakery",
        [DefaultCategoryKey.MeatFish] = "meat_fish",
        [DefaultCategoryKey.Deli] = "deli",
        [DefaultCategoryKey.DairyEggs] = "dairy_eggs",
        [DefaultCategoryKey.Frozen] = "frozen",
        [DefaultCategoryKey.Pantry] = "pantry",
        [DefaultCategoryKey.Drinks] = "drinks",
        [DefaultCategoryKey.Snacks] = "snacks",
        [DefaultCategoryKey.Household] = "household",
        [DefaultCategoryKey.Other] = "other",
    };

    /// <summary>Every default key in its default order (Other last).</summary>
    public static IReadOnlyList<DefaultCategoryKey> All { get; } = Enum.GetValues<DefaultCategoryKey>();

    /// <summary>The default position of a key: 1 for Produce to 11 for Other.</summary>
    public static int DefaultPosition(this DefaultCategoryKey key) => (int)key + 1;

    public static string ToDbValue(this DefaultCategoryKey key) => DbValues[key];

    /// <summary>The key for a stored value; null for null or an unknown value (a custom category).</summary>
    public static DefaultCategoryKey? FromDbValue(string? value)
    {
        foreach (var (key, dbValue) in DbValues)
            if (dbValue == value) return key;
        return null;
    }
}
