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
}
