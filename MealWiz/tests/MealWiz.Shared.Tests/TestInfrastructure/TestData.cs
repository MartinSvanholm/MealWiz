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

    public static GroceryItem GroceryItem(string name, string amount = "", int id = 0, bool isPicked = false) =>
        new() { Id = id, Name = name, Amount = amount, IsPicked = isPicked };

    /// <summary>A plan for <paramref name="date"/>'s week with <paramref name="meal"/> on that date.</summary>
    public static MealPlan MealPlanWith(DateTime date, Meal meal)
    {
        var plan = new MealPlan(date) { Id = 1 };
        meal.MealDate = date.Date;
        meal.MealPlan = plan;
        plan.MealOnDate[date.Date] = meal;
        return plan;
    }
}
