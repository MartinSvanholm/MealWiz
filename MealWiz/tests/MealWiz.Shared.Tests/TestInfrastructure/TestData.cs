using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;

namespace MealWiz.Shared.Tests.TestInfrastructure;

public static class TestData
{
    public static Meal Meal(string name, int id = 1, MealType type = MealType.Regular, string recipe = "", params Ingredient[] ingredients) =>
        new()
        {
            Id = id,
            Name = name,
            Type = type,
            Recipe = recipe,
            Ingredients = [.. ingredients]
        };

    public static Ingredient Ingredient(string name, string amount = "", int id = 0, bool showOnGroceryList = true) =>
        new() { Id = id, Name = name, Amount = amount, ShowOnGroceryList = showOnGroceryList };

    public static GroceryItem GroceryItem(string name, string amount = "", int id = 0, bool isPicked = false, bool isManual = true) =>
        new() { Id = id, Name = name, Amount = amount, IsPicked = isPicked, IsManual = isManual };

    /// <summary>A plan for <paramref name="date"/>'s week with <paramref name="meal"/> on that date.</summary>
    public static MealPlan MealPlanWith(DateTime date, Meal meal, int plannedMealId = 1) =>
        MealPlanWithDays((date, meal, plannedMealId));

    /// <summary>A plan (id 1) for the first day's week with each meal on its date and its planned-day id.</summary>
    public static MealPlan MealPlanWithDays(params (DateTime Date, Meal Meal, int PlannedMealId)[] days)
    {
        var plan = new MealPlan(days[0].Date) { Id = 1 };
        foreach (var (date, meal, plannedMealId) in days)
        {
            meal.MealDate = date.Date;
            meal.MealPlan = plan;
            plan.MealOnDate[date.Date] = meal;
            plan.PlannedMealIdOnDate[date.Date] = plannedMealId;
        }

        return plan;
    }

    public static GroceryItemSource Source(int groceryItemId, int? plannedMealId, int? ingredientId, string amount = "", int id = 0) =>
        new() { Id = id, GroceryItemId = groceryItemId, PlannedMealId = plannedMealId, IngredientId = ingredientId, Amount = amount };

    /// <summary>An item added from the meal plan (not manual), as the stale rule sees it.</summary>
    public static GroceryItem LinkedGroceryItem(string name, int id, string amount = "", bool isPicked = false) =>
        new() { Id = id, Name = name, Amount = amount, IsPicked = isPicked, IsManual = false };

    /// <summary>A source whose planned day now holds another meal, so it is stale.</summary>
    public static GroceryItemSource StaleSource(int id, int groceryItemId, string? mealName) =>
        new()
        {
            Id = id, GroceryItemId = groceryItemId, PlannedMealId = 100 + id, IngredientId = 200 + id,
            PlannedDayMealId = 2, IngredientMealId = 1, MealName = mealName
        };

    /// <summary>A source whose planned day still holds the ingredient's meal.</summary>
    public static GroceryItemSource CurrentSource(int id, int groceryItemId, string? mealName = "Current") =>
        new()
        {
            Id = id, GroceryItemId = groceryItemId, PlannedMealId = 100 + id, IngredientId = 200 + id,
            PlannedDayMealId = 1, IngredientMealId = 1, MealName = mealName
        };

    public static GroceryItemSource ChangedSource(int groceryItemId, int plannedMealId, int ingredientId, string oldAmount, string newAmount, int id = 0) =>
        new()
        {
            Id = id, GroceryItemId = groceryItemId, PlannedMealId = plannedMealId, IngredientId = ingredientId,
            Amount = oldAmount, IngredientAmount = newAmount, PlannedDayMealId = 1, IngredientMealId = 1
        };
}
