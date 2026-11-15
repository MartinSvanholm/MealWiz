namespace MealWiz.Shared.Features.GroceryCategories.Models;

/// <summary>How the sections are ordered: learned from shopping trips, or set by hand on the Categories page.</summary>
public enum SectionOrderMode
{
    Learned,
    Manual,
}

public static class SectionOrderModeExtensions
{
    public const string LearnedValue = "learned";
    public const string ManualValue = "manual";

    public static string ToDbValue(this SectionOrderMode mode) => mode == SectionOrderMode.Manual ? ManualValue : LearnedValue;

    public static SectionOrderMode FromDbValue(string? value) => value == ManualValue ? SectionOrderMode.Manual : SectionOrderMode.Learned;
}

/// <summary>
/// The user's grocery list settings, stored on the account. <see cref="LearnedUntil"/> is the last tick the section
/// order has learned from. A user without a row has the defaults.
/// </summary>
public record GroceryListSettings(SectionOrderMode OrderMode = SectionOrderMode.Learned, DateTime? LearnedUntil = null)
{
    public GroceryListSettings(GroceryListSettingsDb db) : this(SectionOrderModeExtensions.FromDbValue(db.OrderMode), db.LearnedUntil)
    {
    }
}
