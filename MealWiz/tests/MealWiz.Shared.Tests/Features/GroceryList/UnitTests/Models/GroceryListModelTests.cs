using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.Ingredients.Models;
using Xunit;
using GroceryListModel = MealWiz.Shared.Features.GroceryList.Models.GroceryList;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

public class GroceryListModelTests
{
    [Fact]
    public void GroceryItem_RoundTripsThroughDbModel()
    {
        var original = new GroceryItemDb
        {
            Id = 4,
            Name = "milk",
            Amount = "1 l",
            IsPicked = true,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc)
        };

        var db = new GroceryItem(original).MapToDb();

        Assert.Equal(original.Id, db.Id);
        Assert.Equal(original.Name, db.Name);
        Assert.Equal(original.Amount, db.Amount);
        Assert.Equal(original.IsPicked, db.IsPicked);
        Assert.Equal(original.CreatedBy, db.CreatedBy);
        Assert.Equal(original.CreatedAt, db.CreatedAt);
        Assert.Equal(original.UpdatedAt, db.UpdatedAt);
    }

    [Fact]
    public void GroceryItem_FromDbModelWithNulls_HasEmptyAmountAndNoUpdatedAt()
    {
        var item = new GroceryItem(new GroceryItemDb { Name = "milk", Amount = null, UpdatedAt = null });

        Assert.Equal(string.Empty, item.Amount);
        Assert.Null(item.UpdatedAt);
        Assert.Null(item.MapToDb().UpdatedAt);
    }

    [Fact]
    public void GroceryItem_Default_HasEmptyNameAndAmount()
    {
        var item = new GroceryItem();

        Assert.Equal(0, item.Id);
        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(string.Empty, item.Amount);
        Assert.False(item.IsPicked);
    }

    [Fact]
    public void GroceryItem_FromIngredient_IsAnUnpickedUnsavedCopy()
    {
        var ingredient = new Ingredient { Id = 9, Name = "pasta", Amount = "500 g", CreatedBy = Guid.NewGuid() };
        var before = DateTime.UtcNow;

        var item = new GroceryItem(ingredient);

        Assert.Equal(0, item.Id);
        Assert.Equal("pasta", item.Name);
        Assert.Equal("500 g", item.Amount);
        Assert.Equal(ingredient.CreatedBy, item.CreatedBy);
        Assert.False(item.IsPicked);
        Assert.InRange(item.CreatedAt, before, DateTime.UtcNow);
        Assert.Null(item.UpdatedAt);
    }

    [Fact]
    public void GroceryList_Default_HasNoItems()
    {
        Assert.Empty(new GroceryListModel().Items);
    }

    [Fact]
    public void SortItemsByIsPicked_PutsUnpickedFirstAndKeepsTheirOrder()
    {
        var list = new GroceryListModel
        {
            Items =
            [
                new GroceryItem { Name = "a", IsPicked = true },
                new GroceryItem { Name = "b" },
                new GroceryItem { Name = "c", IsPicked = true },
                new GroceryItem { Name = "d" }
            ]
        };

        list.SortItemsByIsPicked();

        Assert.Equal(new[] { "b", "d", "a", "c" }, list.Items.Select(i => i.Name));
    }
}
