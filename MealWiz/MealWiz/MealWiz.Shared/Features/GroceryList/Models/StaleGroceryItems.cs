namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>
/// Items from meals no longer in the plan: not picked, not manual, with at least one source, and every source stale.
/// </summary>
public static class StaleGroceryItems
{
    public const string OtherItemsHeading = "Other items";

    /// <summary>The stale items, in the order given.</summary>
    public static List<GroceryItem> Find(IEnumerable<GroceryItem> items, IEnumerable<GroceryItemSource> sources)
    {
        var bySource = sources.ToLookup(s => s.GroceryItemId);
        return items
            .Where(i => !i.IsPicked && !i.IsManual && bySource[i.Id].Any() && bySource[i.Id].All(s => s.IsStale))
            .ToList();
    }

    /// <summary>
    /// Groups the items by the meal name of their lowest-id stale source that has one; none means "Other items".
    /// Groups sort by heading with "Other items" last; items by name then id.
    /// </summary>
    public static List<GroceryItemGroup> GroupByMeal(IEnumerable<GroceryItem> staleItems, IEnumerable<GroceryItemSource> sources)
    {
        var staleByItem = sources.Where(s => s.IsStale).ToLookup(s => s.GroceryItemId);

        return staleItems
            .GroupBy(item => staleByItem[item.Id]
                .Where(s => s.MealName is not null)
                .OrderBy(s => s.Id)
                .Select(s => s.MealName)
                .FirstOrDefault() ?? OtherItemsHeading)
            .OrderBy(g => g.Key == OtherItemsHeading)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GroceryItemGroup(
                g.Key,
                g.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Id).ToList()))
            .ToList();
    }
}
