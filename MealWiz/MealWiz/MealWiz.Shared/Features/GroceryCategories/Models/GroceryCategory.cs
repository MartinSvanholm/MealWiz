namespace MealWiz.Shared.Features.GroceryCategories.Models;

/// <summary>
/// A section of the grocery list. A default category has a <see cref="DefaultKey"/> and, until the user renames it,
/// no <see cref="Name"/> (it is shown in the UI language); a custom category has a name and no key.
/// </summary>
public class GroceryCategory
{
    public GroceryCategory()
    {
    }

    public GroceryCategory(GroceryCategoryDb db)
    {
        Id = db.Id;
        DefaultKey = DefaultCategoryKeyExtensions.FromDbValue(db.DefaultKey);
        Name = db.Name;
        Emoji = db.Emoji;
        Position = db.Position;
        OrderScore = db.OrderScore;
        AbsorbedKeys = (db.AbsorbedKeys ?? [])
            .Select(DefaultCategoryKeyExtensions.FromDbValue)
            .OfType<DefaultCategoryKey>()
            .ToList();
        CreatedBy = db.CreatedBy;
        CreatedAt = db.CreatedAt;
        UpdatedAt = db.UpdatedAt;
    }

    public int Id { get; set; }
    public DefaultCategoryKey? DefaultKey { get; set; }
    public string? Name { get; set; }
    public string? Emoji { get; set; }
    public int Position { get; set; }

    /// <summary>The learned, fractional place (MEA-139); null means <see cref="Position"/>.</summary>
    public double? OrderScore { get; set; }

    /// <summary>
    /// Default keys this category took over when the categories they belonged to were deleted: dictionary words for
    /// those keys land here.
    /// </summary>
    public IReadOnlyList<DefaultCategoryKey> AbsorbedKeys { get; set; } = [];
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>The catch-all section, last on the list; it can be renamed but not deleted.</summary>
    public bool IsOther => DefaultKey == DefaultCategoryKey.Other;

    /// <summary>The category's <c>data-category</c> in the markup: its default key, or its id for a custom category.</summary>
    public string DataKey => DefaultKey?.ToDbValue() ?? Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether dictionary words for <paramref name="key"/> land in this category.</summary>
    public bool Takes(DefaultCategoryKey key) => DefaultKey == key || AbsorbedKeys.Contains(key);

    /// <summary>A new, unsaved default category at its default position.</summary>
    public static GroceryCategory Default(DefaultCategoryKey key) => new() { DefaultKey = key, Position = key.DefaultPosition() };

    public GroceryCategoryDb MapToDb() => new()
    {
        Id = Id,
        DefaultKey = DefaultKey?.ToDbValue(),
        Name = Name,
        Emoji = Emoji,
        Position = Position,
        OrderScore = OrderScore,
        AbsorbedKeys = AbsorbedKeys.Select(k => k.ToDbValue()).ToList(),
        CreatedBy = CreatedBy,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
    };
}
