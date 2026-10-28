using MealWiz.Shared.Features.Ingredients.Models;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.UnitTests.Models;

public class IngredientModelTests
{
    [Fact]
    public void Ingredient_RoundTripsThroughDbModel()
    {
        var original = new IngredientDb
        {
            Id = 3,
            Name = "pasta",
            Amount = "500 g",
            ShowOnGroceryList = true,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
            MealId = 7
        };

        var db = new Ingredient(original).MapToIngredientDb();

        Assert.Equal(original.Id, db.Id);
        Assert.Equal(original.Name, db.Name);
        Assert.Equal(original.Amount, db.Amount);
        Assert.Equal(original.ShowOnGroceryList, db.ShowOnGroceryList);
        Assert.Equal(original.CreatedBy, db.CreatedBy);
        Assert.Equal(original.CreatedAt, db.CreatedAt);
        Assert.Equal(original.UpdatedAt, db.UpdatedAt);
        Assert.Equal(original.MealId, db.MealId);
    }

    [Fact]
    public void Ingredient_Default_IsUnsaved()
    {
        var db = new Ingredient().MapToIngredientDb();

        Assert.Equal(0, db.Id);
        Assert.Equal(0, db.MealId);
        Assert.False(db.ShowOnGroceryList);
        Assert.Null(db.Name);
    }
}
