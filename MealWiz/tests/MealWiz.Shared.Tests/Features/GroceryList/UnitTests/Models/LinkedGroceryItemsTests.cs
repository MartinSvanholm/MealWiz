using MealWiz.Shared.Features.GroceryList.Models;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

/// <summary>Sorting a deleted meal's or ingredient's grocery items into to-buy, basket and links to drop (MEA-93).</summary>
public class LinkedGroceryItemsTests
{
    private const int Meal = 5;
    private const int OtherMeal = 6;

    private static GroceryItem Item(int id, bool picked = false, bool manual = false) =>
        new() { Id = id, Name = $"item {id}", IsPicked = picked, IsManual = manual };

    /// <summary>A live source: the day holds the ingredient's meal unless <paramref name="dayMealId"/> says otherwise.</summary>
    private static GroceryItemSource Source(int id, int itemId, int mealId = Meal, int ingredientId = 100, int? dayMealId = null) =>
        new()
        {
            Id = id, GroceryItemId = itemId, PlannedMealId = 500 + id, IngredientId = ingredientId,
            IngredientMealId = mealId, PlannedDayMealId = dayMealId ?? mealId
        };

    [Fact]
    public void ForMeal_ToBuyPlanItemLinkedOnlyToTheMeal_IsToBuy()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1)], [Source(11, 1)]);

        Assert.Equal([1], links.ToBuy.Select(i => i.Id));
        Assert.Empty(links.InBasket);
        Assert.Empty(links.SourceIdsToDrop);
    }

    [Fact]
    public void ForMeal_PickedPlanItem_IsInBasket()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1, picked: true)], [Source(11, 1)]);

        Assert.Equal([1], links.InBasket.Select(i => i.Id));
        Assert.Empty(links.ToBuy);
    }

    [Fact]
    public void ForMeal_ManualItem_DropsOnlyItsSources()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1, manual: true)], [Source(11, 1)]);

        Assert.Equal([11], links.SourceIdsToDrop);
        Assert.Empty(links.ToBuy);
        Assert.Empty(links.InBasket);
    }

    [Fact]
    public void ForMeal_ItemAnotherPlannedMealNeeds_DropsOnlyTheMealsSources()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1)], [Source(11, 1), Source(12, 1, mealId: OtherMeal)]);

        Assert.Equal([11], links.SourceIdsToDrop);
        Assert.Empty(links.ToBuy);
    }

    [Fact]
    public void ForMeal_ItemWhoseOtherSourceIsStale_IsToBuy()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1)], [Source(11, 1), Source(12, 1, mealId: OtherMeal, dayMealId: 9)]);

        Assert.Equal([1], links.ToBuy.Select(i => i.Id));
        Assert.Empty(links.SourceIdsToDrop);
    }

    [Fact]
    public void ForMeal_SourceFromADaySwappedAwayFromTheMeal_StillCounts()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1)], [Source(11, 1, dayMealId: OtherMeal)]);

        Assert.Equal([1], links.ToBuy.Select(i => i.Id));
    }

    [Fact]
    public void ForMeal_DayNowHoldingTheMealWithAnotherMealsIngredient_IsNotLinked()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1)], [Source(11, 1, mealId: OtherMeal, dayMealId: Meal)]);

        Assert.Empty(links.ToBuy);
        Assert.Empty(links.InBasket);
        Assert.Empty(links.SourceIdsToDrop);
    }

    [Fact]
    public void ForMeal_NoLinkedSources_IsEmpty()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1), Item(2, manual: true)], []);

        Assert.Empty(links.ToBuy);
        Assert.Empty(links.InBasket);
        Assert.Empty(links.SourceIdsToDrop);
    }

    [Fact]
    public void ForIngredient_AnotherIngredientOfTheSameMealOnTheItem_DropsOnlyThisIngredientsSources()
    {
        var links = LinkedGroceryItems.ForIngredient(100, [Item(1)], [Source(11, 1, ingredientId: 100), Source(12, 1, ingredientId: 101)]);

        Assert.Equal([11], links.SourceIdsToDrop);
        Assert.Empty(links.ToBuy);
    }

    [Fact]
    public void ForIngredient_ToBuyItemLinkedOnlyToTheIngredient_IsToBuy()
    {
        var links = LinkedGroceryItems.ForIngredient(100, [Item(1), Item(2)], [Source(11, 1, ingredientId: 100), Source(12, 2, ingredientId: 101)]);

        Assert.Equal([1], links.ToBuy.Select(i => i.Id));
    }

    [Fact]
    public void Sort_SourceOfAnItemNotOnTheList_IsIgnored()
    {
        var links = LinkedGroceryItems.ForMeal(Meal, [Item(1)], [Source(11, 99)]);

        Assert.Empty(links.ToBuy);
        Assert.Empty(links.InBasket);
        Assert.Empty(links.SourceIdsToDrop);
    }
}
