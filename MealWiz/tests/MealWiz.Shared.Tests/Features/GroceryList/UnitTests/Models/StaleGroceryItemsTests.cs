using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

public class StaleGroceryItemsTests
{
    private static List<int> FindIds(IEnumerable<GroceryItem> items, params GroceryItemSource[] sources) =>
        StaleGroceryItems.Find(items, sources).Select(i => i.Id).ToList();

    [Fact]
    public void Find_ItemWhoseOnlySourceIsStale_IsStale()
    {
        var ids = FindIds([TestData.LinkedGroceryItem("Flour", 1)], TestData.StaleSource(1, 1, "Tacos"));

        Assert.Equal([1], ids);
    }

    [Theory]
    [InlineData("planned day gone")]
    [InlineData("ingredient gone")]
    [InlineData("day holds another meal")]
    public void Find_EachStaleReason_IsStale(string reason)
    {
        var source = TestData.CurrentSource(1, 1);
        switch (reason)
        {
            case "planned day gone": source.PlannedMealId = null; break;
            case "ingredient gone": source.IngredientId = null; break;
            default: source.PlannedDayMealId = 2; break;
        }

        Assert.Equal([1], FindIds([TestData.LinkedGroceryItem("Flour", 1)], source));
    }

    [Fact]
    public void Find_AllOfSeveralSourcesStale_IsStale()
    {
        var ids = FindIds([TestData.LinkedGroceryItem("Flour", 1)],
            TestData.StaleSource(1, 1, "Tacos"), TestData.StaleSource(2, 1, "Soup"));

        Assert.Equal([1], ids);
    }

    [Fact]
    public void Find_OneStaleAndOneCurrentSource_IsNotStale()
    {
        var ids = FindIds([TestData.LinkedGroceryItem("Flour", 1)],
            TestData.StaleSource(1, 1, "Tacos"), TestData.CurrentSource(2, 1));

        Assert.Empty(ids);
    }

    [Fact]
    public void Find_PickedItem_IsNotStale() =>
        Assert.Empty(FindIds([TestData.LinkedGroceryItem("Flour", 1, isPicked: true)], TestData.StaleSource(1, 1, "Tacos")));

    [Fact]
    public void Find_ManualItemWithStaleSources_IsNotStale() =>
        Assert.Empty(FindIds([TestData.GroceryItem("Flour", id: 1, isManual: true)], TestData.StaleSource(1, 1, "Tacos")));

    [Fact]
    public void Find_ItemWithoutSources_IsNotStale() =>
        Assert.Empty(FindIds([TestData.LinkedGroceryItem("Flour", 1)]));

    [Fact]
    public void Find_SourcesOfOtherItems_DoNotCount() =>
        Assert.Empty(FindIds([TestData.LinkedGroceryItem("Flour", 1)], TestData.StaleSource(1, 2, "Tacos")));

    [Fact]
    public void Find_KeepsTheItemsOrder()
    {
        var ids = FindIds(
            [TestData.LinkedGroceryItem("Z", 3), TestData.LinkedGroceryItem("A", 1), TestData.LinkedGroceryItem("M", 2)],
            TestData.StaleSource(1, 1, "T"), TestData.StaleSource(2, 2, "T"), TestData.StaleSource(3, 3, "T"));

        Assert.Equal([3, 1, 2], ids);
    }

    [Fact]
    public void Find_Empty_ReturnsEmpty() => Assert.Empty(FindIds([]));

    [Fact]
    public void GroupByMeal_GroupsByMealNameSortedWithItemsByName()
    {
        var items = new[]
        {
            TestData.LinkedGroceryItem("Salsa", 1), TestData.LinkedGroceryItem("Leeks", 2), TestData.LinkedGroceryItem("tortillas", 3)
        };
        var sources = new[]
        {
            TestData.StaleSource(1, 1, "Tacos"), TestData.StaleSource(2, 2, "Soup"), TestData.StaleSource(3, 3, "Tacos")
        };

        var groups = StaleGroceryItems.GroupByMeal(items, sources);

        Assert.Equal(["Soup", "Tacos"], groups.Select(g => g.Heading));
        Assert.Equal(["Salsa", "tortillas"], groups[1].Items.Select(i => i.Name));
    }

    [Fact]
    public void GroupByMeal_ItemFromTwoOldMeals_GoesUnderItsLowestIdStaleSourcesMeal()
    {
        var items = new[] { TestData.LinkedGroceryItem("Salt", 1) };
        var sources = new[] { TestData.StaleSource(9, 1, "Soup"), TestData.StaleSource(4, 1, "Tacos") };

        var group = Assert.Single(StaleGroceryItems.GroupByMeal(items, sources));

        Assert.Equal("Tacos", group.Heading);
        Assert.Single(group.Items);
    }

    [Fact]
    public void GroupByMeal_NullMealName_GoesUnderOtherItemsLast()
    {
        var items = new[] { TestData.LinkedGroceryItem("Salt", 1), TestData.LinkedGroceryItem("Zest", 2) };
        var sources = new[] { TestData.StaleSource(1, 1, null), TestData.StaleSource(2, 2, "Zebra stew") };

        var groups = StaleGroceryItems.GroupByMeal(items, sources);

        Assert.Equal(["Zebra stew", null], groups.Select(g => g.Heading));
    }

    [Fact]
    public void GroupByMeal_PrefersANamedSourceOverANullOne()
    {
        var items = new[] { TestData.LinkedGroceryItem("Salt", 1) };
        var sources = new[] { TestData.StaleSource(1, 1, null), TestData.StaleSource(2, 1, "Soup") };

        var group = Assert.Single(StaleGroceryItems.GroupByMeal(items, sources));

        Assert.Equal("Soup", group.Heading);
    }

    [Fact]
    public void GroupByMeal_Empty_ReturnsEmpty() =>
        Assert.Empty(StaleGroceryItems.GroupByMeal([], []));
}
