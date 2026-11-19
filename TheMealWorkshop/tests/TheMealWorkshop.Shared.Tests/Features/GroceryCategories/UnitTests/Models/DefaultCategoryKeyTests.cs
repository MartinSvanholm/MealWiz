using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.UnitTests.Models;

public class DefaultCategoryKeyTests
{
    [Fact]
    public void All_IsTheSupermarketOrder_WithOtherLast()
    {
        Assert.Equal(
            ["produce", "bakery", "meat_fish", "deli", "dairy_eggs", "frozen", "pantry", "drinks", "snacks", "household", "other"],
            DefaultCategoryKeyExtensions.All.Select(k => k.ToDbValue()));
        Assert.Equal(Enumerable.Range(1, 11), DefaultCategoryKeyExtensions.All.Select(k => k.DefaultPosition()));
    }

    [Fact]
    public void FromDbValue_RoundTrips_AndUnknownOrNullIsNoKey()
    {
        foreach (var key in DefaultCategoryKeyExtensions.All)
            Assert.Equal(key, DefaultCategoryKeyExtensions.FromDbValue(key.ToDbValue()));

        Assert.Null(DefaultCategoryKeyExtensions.FromDbValue(null));
        Assert.Null(DefaultCategoryKeyExtensions.FromDbValue("sweets"));
    }

    [Fact]
    public void Category_MapsToAndFromItsRow()
    {
        var category = new GroceryCategory { Id = 4, DefaultKey = DefaultCategoryKey.DairyEggs, Name = "Milk stuff", Emoji = "🥛", Position = 5 };

        var row = category.MapToDb();
        var back = new GroceryCategory(row);

        Assert.Equal("dairy_eggs", row.DefaultKey);
        Assert.Equal((4, DefaultCategoryKey.DairyEggs, "Milk stuff", "🥛", 5), (back.Id, back.DefaultKey, back.Name, back.Emoji, back.Position));
        Assert.False(back.IsOther);
        Assert.True(GroceryCategory.Default(DefaultCategoryKey.Other).IsOther);
    }
}
