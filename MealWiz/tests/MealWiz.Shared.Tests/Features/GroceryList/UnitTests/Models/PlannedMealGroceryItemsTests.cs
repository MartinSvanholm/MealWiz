using MealWiz.Shared.Features.GroceryList.Models;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

public class PlannedMealGroceryItemsTests
{
    private const int ThisDay = 501;
    private const int OtherDay = 502;

    private static GroceryItem PlanItem(int id, bool isPicked = false, bool isManual = false) => new()
    {
        Id = id,
        Name = $"Item {id}",
        IsPicked = isPicked,
        IsManual = isManual
    };

    private static GroceryItemSource Source(int itemId, int? plannedMealId, bool stale = false) => new()
    {
        GroceryItemId = itemId,
        PlannedMealId = plannedMealId,
        IngredientId = 1,
        PlannedDayMealId = stale ? 8 : 7,
        IngredientMealId = 7
    };

    private static List<int> Ids(IEnumerable<GroceryItem> items, params GroceryItemSource[] sources)
        => PlannedMealGroceryItems.OnlyNeededBy(ThisDay, items, sources).ConvertAll(i => i.Id);

    [Fact]
    public void OnlyNeededBy_ItemLinkedOnlyToThisPlannedDay_IsIncluded()
        => Assert.Equal([1], Ids([PlanItem(1)], Source(1, ThisDay)));

    [Fact]
    public void OnlyNeededBy_ItemAlsoLinkedToAnotherLivePlannedDay_IsLeftOut()
        => Assert.Empty(Ids([PlanItem(1)], Source(1, ThisDay), Source(1, OtherDay)));

    [Fact]
    public void OnlyNeededBy_OtherSourcesAreStale_IsIncluded()
        => Assert.Equal([1], Ids([PlanItem(1)], Source(1, ThisDay), Source(1, OtherDay, stale: true), Source(1, null)));

    [Fact]
    public void OnlyNeededBy_ThisDaysSourceIsStale_IsLeftOut()
        => Assert.Empty(Ids([PlanItem(1)], Source(1, ThisDay, stale: true)));

    [Fact]
    public void OnlyNeededBy_PickedItem_IsLeftOut()
        => Assert.Empty(Ids([PlanItem(1, isPicked: true)], Source(1, ThisDay)));

    [Fact]
    public void OnlyNeededBy_ManualItemWithASourceFromThisDay_IsLeftOut()
        => Assert.Empty(Ids([PlanItem(1, isManual: true)], Source(1, ThisDay)));

    [Fact]
    public void OnlyNeededBy_ItemWithoutASourceFromThisDay_IsLeftOut()
        => Assert.Empty(Ids([PlanItem(1), PlanItem(2)], Source(2, OtherDay)));

    [Fact]
    public void OnlyNeededBy_TwoSourcesFromThisDayOnOneItem_ReturnsItOnce()
        => Assert.Equal([1], Ids([PlanItem(1)], Source(1, ThisDay), Source(1, ThisDay)));

    [Fact]
    public void OnlyNeededBy_KeepsTheOrderOfTheItems()
        => Assert.Equal([3, 1, 2], Ids([PlanItem(3), PlanItem(1), PlanItem(2)], Source(1, ThisDay), Source(2, ThisDay), Source(3, ThisDay)));

    // ---- SourceIdsOnSharedItems (MEA-99) ----

    private static GroceryItemSource SourceWithId(int id, int itemId, int? plannedMealId, bool stale = false)
    {
        var source = Source(itemId, plannedMealId, stale);
        source.Id = id;
        return source;
    }

    private static List<int> SharedIds(IEnumerable<GroceryItem> items, params GroceryItemSource[] sources)
        => PlannedMealGroceryItems.SourceIdsOnSharedItems(ThisDay, items, sources);

    [Fact]
    public void SourceIdsOnSharedItems_ItemAlsoLinkedToAnotherLivePlannedDay_ReturnsThisDaysSource()
        => Assert.Equal([21], SharedIds([PlanItem(1)], SourceWithId(21, 1, ThisDay), SourceWithId(22, 1, OtherDay)));

    [Fact]
    public void SourceIdsOnSharedItems_ManualItemWithASourceFromThisDay_ReturnsIt()
        => Assert.Equal([21], SharedIds([PlanItem(1, isManual: true)], SourceWithId(21, 1, ThisDay)));

    [Fact]
    public void SourceIdsOnSharedItems_ItemOnlyThisDayNeeds_ReturnsNothing()
        => Assert.Empty(SharedIds([PlanItem(1)], SourceWithId(21, 1, ThisDay)));

    [Fact]
    public void SourceIdsOnSharedItems_PickedItem_ReturnsNothing()
        => Assert.Empty(SharedIds([PlanItem(1, isPicked: true)], SourceWithId(21, 1, ThisDay), SourceWithId(22, 1, OtherDay)));

    [Fact]
    public void SourceIdsOnSharedItems_ThisDaysSourceIsStale_ReturnsNothing()
        => Assert.Empty(SharedIds([PlanItem(1)], SourceWithId(21, 1, ThisDay, stale: true), SourceWithId(22, 1, OtherDay)));

    [Fact]
    public void SourceIdsOnSharedItems_OnlyOtherDaysSources_ReturnsNothing()
        => Assert.Empty(SharedIds([PlanItem(1)], SourceWithId(22, 1, OtherDay)));

    [Fact]
    public void SourceIdsOnSharedItems_TwoSourcesFromThisDayOnASharedItem_ReturnsBoth()
        => Assert.Equal([21, 23], SharedIds([PlanItem(1)], SourceWithId(21, 1, ThisDay), SourceWithId(22, 1, OtherDay), SourceWithId(23, 1, ThisDay)));

    [Fact]
    public void SourceIdsOnSharedItems_SourceOfAnItemNotOnTheList_IsIgnored()
        => Assert.Empty(SharedIds([PlanItem(1)], SourceWithId(21, 9, ThisDay), SourceWithId(22, 9, OtherDay)));
}
