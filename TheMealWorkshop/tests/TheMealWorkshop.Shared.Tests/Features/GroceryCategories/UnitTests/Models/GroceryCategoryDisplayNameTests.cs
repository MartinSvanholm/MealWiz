using TheMealWorkshop.Shared.Features.GroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Tests.TestInfrastructure;
using Microsoft.Extensions.Localization;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.UnitTests.Models;

/// <summary>A default category is named in the UI language until the user renames it; the user's own text is shown as typed.</summary>
public class GroceryCategoryDisplayNameTests
{
    private static readonly IStringLocalizer<GroceryCategoriesResource> Localizer =
        TestLocalizer.For<GroceryCategoriesResource>();

    [Theory]
    [InlineData(DefaultCategoryKey.Produce, "Fruit & vegetables")]
    [InlineData(DefaultCategoryKey.DairyEggs, "Dairy & eggs")]
    [InlineData(DefaultCategoryKey.Household, "Household & personal care")]
    [InlineData(DefaultCategoryKey.Other, "Other")]
    public void DefaultCategory_InEnglish(DefaultCategoryKey key, string expected) =>
        Assert.Equal(expected, GroceryCategory.Default(key).DisplayName(Localizer));

    [Theory]
    [InlineData(DefaultCategoryKey.Produce, "Frugt & grønt")]
    [InlineData(DefaultCategoryKey.DairyEggs, "Mejeri & æg")]
    [InlineData(DefaultCategoryKey.Pantry, "Kolonial")]
    [InlineData(DefaultCategoryKey.Other, "Andet")]
    public void DefaultCategory_InDanish(DefaultCategoryKey key, string expected)
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal(expected, GroceryCategory.Default(key).DisplayName(Localizer));
    }

    [Fact]
    public void EveryDefaultKey_HasANameInBothLanguages()
    {
        foreach (var key in DefaultCategoryKeyExtensions.All)
        {
            var resourceKey = GroceryCategoryDisplayName.ResourceKey(key);
            Assert.False(Localizer[resourceKey].ResourceNotFound, $"{resourceKey} (en)");
            using var _ = CultureScope.Use("da");
            Assert.False(Localizer[resourceKey].ResourceNotFound, $"{resourceKey} (da)");
        }
    }

    [Fact]
    public void RenamedDefaultCategory_ShowsTheUsersText_InEitherLanguage()
    {
        var category = new GroceryCategory { DefaultKey = DefaultCategoryKey.Produce, Name = "Grønt" };

        Assert.Equal("Grønt", category.DisplayName(Localizer));
        using var _ = CultureScope.Use("da");
        Assert.Equal("Grønt", category.DisplayName(Localizer));
    }

    [Fact]
    public void CustomCategory_ShowsItsName()
    {
        Assert.Equal("Health food", new GroceryCategory { Name = "Health food" }.DisplayName(Localizer));
    }

    [Fact]
    public void Emoji_GoesBeforeTheName()
    {
        var category = new GroceryCategory { DefaultKey = DefaultCategoryKey.Bakery, Emoji = "🥖" };

        Assert.Equal("🥖 Bakery", category.DisplayName(Localizer));
    }
}
