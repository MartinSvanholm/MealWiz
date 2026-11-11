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

    /// <summary>
    /// This planned day's live sources on to-buy items it doesn't alone need (shared or manual items): the links the
    /// remove and swap drawers drop so those items' amounts go down (MEA-99). In source order.
    /// </summary>
    public static List<int> SourceIdsOnSharedItems(int plannedMealId, IEnumerable<GroceryItem> items, IEnumerable<GroceryItemSource> sources)
    {
        var itemList = items.ToList();
        var sourceList = sources.ToList();
        var onlyThisDay = OnlyNeededBy(plannedMealId, itemList, sourceList).Select(i => i.Id).ToHashSet();
        var byId = itemList.ToDictionary(i => i.Id);

        return sourceList
            .Where(s => s.PlannedMealId == plannedMealId && !s.IsStale
                        && byId.TryGetValue(s.GroceryItemId, out var item)
                        && !item.IsPicked && !onlyThisDay.Contains(item.Id))
            .Select(s => s.Id)
            .ToList();
    }
}
