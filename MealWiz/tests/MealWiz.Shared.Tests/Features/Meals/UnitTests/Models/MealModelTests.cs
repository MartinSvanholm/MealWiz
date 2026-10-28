using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.UnitTests.Models;

public class MealModelTests
{
    private static MealDb MealDb(string mealType = MealTypeExtensions.RegularValue) => new()
    {
        Id = 7,
        Name = "Pasta",
        Recipe = "Boil\nServe",
        CreatedBy = Guid.NewGuid(),
        CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
        MealType = mealType,
        Ingredients =
        [
            new IngredientDb { Id = 1, Name = "pasta", Amount = "500 g", ShowOnGroceryList = true, MealId = 7 },
            new IngredientDb { Id = 2, Name = "salt", Amount = "", ShowOnGroceryList = false, MealId = 7 }
        ]
    };

    [Fact]
    public void Meal_RoundTripsThroughDbModel()
    {
        var original = MealDb(MealTypeExtensions.LeftoverValue);

        var meal = new Meal(original);
        var db = meal.MapToMealDb();

        Assert.Equal(original.Id, db.Id);
        Assert.Equal(original.Name, db.Name);
        Assert.Equal(original.Recipe, db.Recipe);
        Assert.Equal(original.CreatedBy, db.CreatedBy);
        Assert.Equal(original.CreatedAt, db.CreatedAt);
        Assert.Equal(original.UpdatedAt, db.UpdatedAt);
        Assert.Equal(MealTypeExtensions.LeftoverValue, db.MealType);
        Assert.Collection(db.Ingredients,
            i => { Assert.Equal(1, i.Id); Assert.Equal("pasta", i.Name); Assert.Equal("500 g", i.Amount); Assert.True(i.ShowOnGroceryList); Assert.Equal(7, i.MealId); },
            i => { Assert.Equal(2, i.Id); Assert.Equal("salt", i.Name); Assert.Equal("", i.Amount); Assert.False(i.ShowOnGroceryList); Assert.Equal(7, i.MealId); });
    }

    [Fact]
    public void Meal_FromDbModelWithoutIngredients_HasAnEmptyIngredientList()
    {
        var db = MealDb();
        db.Ingredients = null!;

        var meal = new Meal(db);

        Assert.NotNull(meal.Ingredients);
        Assert.Empty(meal.Ingredients);
        Assert.Empty(meal.MapToMealDb().Ingredients);
    }

    [Fact]
    public void Meal_FromMealPlanRow_SetsThePlanAndParsesTheDate()
    {
        var plan = new MealPlan(new DateTime(2026, 10, 14));

        var meal = new Meal(MealDb(MealTypeExtensions.EatOutValue), plan, "2026-10-15");

        Assert.Same(plan, meal.MealPlan);
        Assert.Equal(new DateTime(2026, 10, 15), meal.MealDate);
        Assert.Equal(MealType.EatOut, meal.Type);
        Assert.Equal(2, meal.Ingredients.Count);
    }

    [Fact]
    public void MapToMealDb_DoesNotCarryThePlanOrDate()
    {
        var meal = new Meal(MealDb(), new MealPlan(new DateTime(2026, 10, 14)), "2026-10-15");

        var db = meal.MapToMealDb();

        // The plan link lives in meal_plans-meals, not on the meal row.
        Assert.Equal(meal.Id, db.Id);
        Assert.Equal(MealTypeExtensions.RegularValue, db.MealType);
    }

    [Fact]
    public void Meal_Default_IsAnEmptyRegularMeal()
    {
        var meal = new Meal();

        Assert.Equal(0, meal.Id);
        Assert.Equal(string.Empty, meal.Name);
        Assert.Equal(string.Empty, meal.Recipe);
        Assert.Empty(meal.Ingredients);
        Assert.Equal(MealType.Regular, meal.Type);
        Assert.True(meal.IsRegular);
        Assert.Null(meal.MealPlan);
        Assert.Null(meal.MealDate);
    }

    [Theory]
    [InlineData("Boil\nServe")]
    [InlineData("Boil\r\nServe")]
    [InlineData("Boil\rServe")]
    [InlineData("Boil\n\n\r\nServe\n")]
    public void RecipeSteps_SplitsOnAnyLineBreakAndDropsBlankLines(string recipe)
    {
        var meal = new Meal { Recipe = recipe };

        Assert.Equal(new[] { "Boil", "Serve" }, meal.RecipeSteps);
    }

    [Fact]
    public void RecipeSteps_NullRecipe_IsEmpty()
    {
        var meal = new Meal { Recipe = null! };

        Assert.Empty(meal.RecipeSteps);
    }

    [Theory]
    [InlineData(MealType.Regular, "regular")]
    [InlineData(MealType.Leftover, "leftover")]
    [InlineData(MealType.EatOut, "eat_out")]
    public void MealType_RoundTripsThroughDbValue(MealType type, string dbValue)
    {
        Assert.Equal(dbValue, type.ToDbValue());
        Assert.Equal(type, MealTypeExtensions.FromDbValue(dbValue));
        Assert.Equal(type, new Meal(MealDb(dbValue)).Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("Leftover")]
    public void MealType_UnknownDbValue_FallsBackToRegular(string? dbValue)
    {
        Assert.Equal(MealType.Regular, MealTypeExtensions.FromDbValue(dbValue));
    }

    [Theory]
    [InlineData(MealType.Regular, true)]
    [InlineData(MealType.Leftover, false)]
    [InlineData(MealType.EatOut, false)]
    public void IsRegular_IsTrueOnlyForRegularMeals(MealType type, bool expected)
    {
        Assert.Equal(expected, new Meal { Type = type }.IsRegular);
    }
}
