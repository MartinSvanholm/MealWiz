namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>
/// The grocery items tied to a meal or an ingredient about to be deleted (MEA-93). Each item with a source linked to
/// it lands in the first bucket that fits: kept (manual, or another live link needs it: only its links are dropped),
/// in the basket (unlinked silently), or still to buy (the user decides).
/// </summary>
public sealed record LinkedGroceryItems(
    IReadOnlyList<GroceryItem> ToBuy,
    IReadOnlyList<GroceryItem> InBasket,
    IReadOnlyList<int> SourceIdsToDrop)
{
    public static LinkedGroceryItems Empty { get; } = new([], [], []);

    /// <summary>Items linked through one of the meal's ingredients (whatever day now holds).</summary>
    public static LinkedGroceryItems ForMeal(int mealId, IEnumerable<GroceryItem> items, IEnumerable<GroceryItemSource> sources) =>
        Sort(items, sources, s => s.IngredientMealId == mealId);

    public static LinkedGroceryItems ForIngredient(int ingredientId, IEnumerable<GroceryItem> items, IEnumerable<GroceryItemSource> sources) =>
        Sort(items, sources, s => s.IngredientId == ingredientId);

    private static LinkedGroceryItems Sort(IEnumerable<GroceryItem> items, IEnumerable<GroceryItemSource> sources, Func<GroceryItemSource, bool> isLinked)
    {
        var byItem = sources.ToLookup(s => s.GroceryItemId);
        List<GroceryItem> toBuy = [], inBasket = [];
        List<int> drop = [];

        foreach (var item in items)
        {
            var itemSources = byItem[item.Id].ToList();
            var linked = itemSources.Where(isLinked).ToList();
            if (linked.Count == 0) continue;

            var neededElsewhere = itemSources.Any(s => !isLinked(s) && !s.IsStale);
            if (item.IsManual || neededElsewhere) drop.AddRange(linked.Select(s => s.Id));
            else if (item.IsPicked) inBasket.Add(item);
            else toBuy.Add(item);
        }

        return new LinkedGroceryItems(toBuy, inBasket, drop);
    }
}
