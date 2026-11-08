namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>The grocery items only one planned day needs: what "Also remove N items" removes with it (MEA-81).</summary>
public static class PlannedMealGroceryItems
{
    /// <summary>
    /// Items still to buy, added from the plan, with a live source from <paramref name="plannedMealId"/> and no live source
    /// from another planned day. Each item once, in the order of <paramref name="items"/>.
    /// </summary>
    public static List<GroceryItem> OnlyNeededBy(int plannedMealId, IEnumerable<GroceryItem> items, IEnumerable<GroceryItemSource> sources)
    {
        var sourcesByItem = sources.ToLookup(s => s.GroceryItemId);

        return items.Where(item =>
        {
            if (item.IsPicked || item.IsManual) return false;

            var live = sourcesByItem[item.Id].Where(s => !s.IsStale).ToList();
            return live.Any(s => s.PlannedMealId == plannedMealId) && live.All(s => s.PlannedMealId == plannedMealId);
        }).ToList();
    }
}
