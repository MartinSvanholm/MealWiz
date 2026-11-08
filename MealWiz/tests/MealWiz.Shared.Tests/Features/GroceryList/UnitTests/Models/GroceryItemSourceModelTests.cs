using MealWiz.Shared.Features.GroceryList.Models;
using Xunit;
using static MealWiz.Shared.Features.GroceryList.Models.GroceryItemSourceDetailsDb;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

public class GroceryItemSourceModelTests
{
    private static GroceryItemSourceDetailsDb Details(int plannedDayMealId = 5, int ingredientMealId = 5) => new()
    {
        Id = 3,
        GroceryItemId = 4,
        PlannedMealId = 7,
        IngredientId = 8,
        Amount = "200 g",
        CreatedBy = Guid.NewGuid(),
        CreatedAt = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc),
        PlannedDay = new PlannedDayDb { Id = 7, MealDate = "2026-11-03", MealId = plannedDayMealId, Meal = new SourceMealDb { Id = plannedDayMealId, Name = "Day meal" } },
        Ingredient = new SourceIngredientDb { Id = 8, Name = "Flour", MealId = ingredientMealId, Meal = new SourceMealDb { Id = ingredientMealId, Name = "Ingredient meal" } }
    };

    [Fact]
    public void FromDetailsDb_MapsColumnsAndJoinedFacts()
    {
        var db = Details();

        var source = new GroceryItemSource(db);

        Assert.Equal((3, 4, 7, 8, "200 g"), (source.Id, source.GroceryItemId, source.PlannedMealId, source.IngredientId, source.Amount));
        Assert.Equal(db.CreatedBy, source.CreatedBy);
        Assert.Equal(db.CreatedAt, source.CreatedAt);
        Assert.Equal(5, source.PlannedDayMealId);
        Assert.Equal(new DateTime(2026, 11, 3), source.PlannedDate);
        Assert.Equal(5, source.IngredientMealId);
        Assert.Equal("Flour", source.IngredientName);
        Assert.Equal("Ingredient meal", source.MealName);
    }

    [Fact]
    public void MealName_WithoutIngredient_FallsBackToThePlannedDaysMeal()
    {
        var db = Details();
        db.Ingredient = null;
        db.IngredientId = null;

        var source = new GroceryItemSource(db);

        Assert.Equal("Day meal", source.MealName);
        Assert.Null(source.IngredientName);
    }

    [Fact]
    public void IsStale_NoPlannedDay_True()
    {
        var db = Details();
        db.PlannedDay = null;
        db.PlannedMealId = null;

        Assert.True(new GroceryItemSource(db).IsStale);
    }

    [Fact]
    public void IsStale_NoIngredient_True()
    {
        var db = Details();
        db.Ingredient = null;
        db.IngredientId = null;

        Assert.True(new GroceryItemSource(db).IsStale);
    }

    [Fact]
    public void IsStale_PlannedDayHoldsTheIngredientsMeal_False() =>
        Assert.False(new GroceryItemSource(Details(plannedDayMealId: 5, ingredientMealId: 5)).IsStale);

    [Fact]
    public void IsStale_PlannedDayHoldsAnotherMeal_True() =>
        Assert.True(new GroceryItemSource(Details(plannedDayMealId: 6, ingredientMealId: 5)).IsStale);

    [Fact]
    public void FromDetailsDb_MapsTheIngredientsCurrentAmount()
    {
        var db = Details();
        db.Ingredient!.Amount = "750 g";

        Assert.Equal("750 g", new GroceryItemSource(db).IngredientAmount);
    }

    [Fact]
    public void FromDetailsDb_WithoutIngredient_HasNoIngredientAmount()
    {
        var db = Details();
        db.Ingredient = null;
        db.IngredientId = null;

        Assert.Null(new GroceryItemSource(db).IngredientAmount);
    }

    [Fact]
    public void FromDetailsDb_IngredientWithoutAmount_HasAnEmptyIngredientAmount()
    {
        var db = Details();
        db.Ingredient!.Amount = null;

        Assert.Equal("", new GroceryItemSource(db).IngredientAmount);
    }

    [Theory]
    [InlineData("500 g", "750 g", true)]
    [InlineData("500 g", "500 g", false)]
    [InlineData(" 500 g ", "500 G", false)]
    [InlineData("500 g", "", true)]
    [InlineData("", "500 g", true)]
    [InlineData("500 g", null, false)]
    public void HasChangedAmount_ComparesTrimmedAmountsIgnoringCase(string amount, string? ingredientAmount, bool expected)
    {
        var source = new GroceryItemSource(Details()) { Amount = amount, IngredientAmount = ingredientAmount };

        Assert.Equal(expected, source.HasChangedAmount);
    }

    [Fact]
    public void HasChangedAmount_StaleSource_IsFalse()
    {
        var source = new GroceryItemSource(Details(plannedDayMealId: 6, ingredientMealId: 5)) { Amount = "500 g", IngredientAmount = "750 g" };

        Assert.False(source.HasChangedAmount);
    }

    [Fact]
    public void MapToGroceryItemSourceDb_CopiesTheColumns()
    {
        var source = new GroceryItemSource(Details());

        var db = source.MapToGroceryItemSourceDb();

        Assert.Equal((3, 4, 7, 8, "200 g"), (db.Id, db.GroceryItemId, db.PlannedMealId, db.IngredientId, db.Amount));
        Assert.Equal(source.CreatedBy, db.CreatedBy);
        Assert.Equal(source.CreatedAt, db.CreatedAt);
    }
}
