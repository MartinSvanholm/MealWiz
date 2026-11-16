using Blazored.LocalStorage;
using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Features.GroceryCategories.State;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>
/// "To buy" in supermarket sections: one per category with something to buy, in the user's order, each with a count;
/// the basket stays one row of pills.
/// </summary>
public class GroceryListPageSectionsTests : ComponentTestBase
{
    private readonly GroceryItem _milk = TestData.GroceryItem("Milk", id: 1);
    private readonly GroceryItem _apples = TestData.GroceryItem("Apples", id: 2);
    private readonly GroceryItem _maelk = TestData.GroceryItem("mælk", id: 3);
    private readonly GroceryItem _flimflam = TestData.GroceryItem("Flimflam", id: 4);
    private readonly GroceryItem _bread = TestData.GroceryItem("Bread", id: 5);

    public GroceryListPageSectionsTests()
    {
        HaveItems(_milk, _apples, _maelk, _flimflam, _bread);
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
    }

    private void HaveItems(params GroceryItem[] items) =>
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items.ToList()));

    private static List<string?> SectionKeys(IRenderedComponent<GroceryListPage> cut) =>
        cut.FindAll(TestId("grocery-category")).Select(s => s.GetAttribute("data-category")).ToList();

    private static AngleSharp.Dom.IElement Section(IRenderedComponent<GroceryListPage> cut, string key) =>
        cut.FindAll(TestId("grocery-category")).Single(s => s.GetAttribute("data-category") == key);

    private static List<string> NamesIn(AngleSharp.Dom.IElement section) =>
        section.QuerySelectorAll(TestId("grocery-item-name")).Select(n => n.TextContent.Trim()).ToList();

    [Fact]
    public void ToBuy_IsGroupedInSections_InTheUsersOrder_WithNamesAndCounts()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Equal(["produce", "bakery", "dairy_eggs", "other"], SectionKeys(cut));
        var dairy = Section(cut, "dairy_eggs");
        Assert.Equal("Dairy & eggs", dairy.QuerySelector(TestId("grocery-category-name"))!.TextContent.Trim());
        Assert.Equal("2", dairy.QuerySelector(TestId("grocery-category-count"))!.TextContent.Trim());
        Assert.Equal(["Milk", "mælk"], NamesIn(dairy));
        Assert.Equal(["Flimflam"], NamesIn(Section(cut, "other")));
        Assert.Equal("Other", Section(cut, "other").QuerySelector(TestId("grocery-category-name"))!.TextContent.Trim());
    }

    [Fact]
    public void ToBuy_FollowsThePositions_NotTheDefaultOrder()
    {
        var categories = TestData.DefaultGroceryCategories();
        categories.Single(c => c.DefaultKey == DefaultCategoryKey.DairyEggs).Position = 0;
        SetupSend<GetGroceryCategories.Query, Result<List<GroceryCategory>>>(Result.Ok(categories.OrderBy(c => c.Position).ToList()));

        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Equal(["dairy_eggs", "produce", "bakery", "other"], SectionKeys(cut));
    }

    [Fact]
    public void SectionsWithNothingToBuy_AreNotShown()
    {
        _bread.IsPicked = true;

        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.DoesNotContain("bakery", SectionKeys(cut));
        Assert.DoesNotContain("frozen", SectionKeys(cut));
    }

    [Fact]
    public void TickingOffTheLastItemOfASection_RemovesTheSection_AndPutsItInTheBasket()
    {
        var cut = RenderWithPopovers<GroceryListPage>();
        var checkbox = Section(cut, "bakery").QuerySelector(TestId("grocery-item-checkbox"))!;

        checkbox.Click();

        cut.WaitForAssertion(() => Assert.Equal(["produce", "dairy_eggs", "other"], SectionKeys(cut)));
        var pills = cut.FindAll($"{TestId("grocery-basket")} {TestId("grocery-item")}").Select(p => p.TextContent.Trim());
        Assert.Equal(["Bread"], pills);
    }

    [Fact]
    public void Basket_StaysOneUngroupedRowOfPills()
    {
        _milk.IsPicked = true;
        _apples.IsPicked = true;

        var cut = RenderWithPopovers<GroceryListPage>();

        var basket = cut.Find(TestId("grocery-basket"));
        Assert.Empty(basket.QuerySelectorAll(TestId("grocery-category")));
        Assert.Equal(2, basket.QuerySelectorAll(TestId("grocery-item")).Length);
        Assert.Null(basket.Closest(TestId("grocery-category")));
    }

    [Fact]
    public void UserWithNoCategories_GetsTheDefaultsCreated()
    {
        SetupSend<GetGroceryCategories.Query, Result<List<GroceryCategory>>>(Result.Ok(new List<GroceryCategory>()));
        SetupSend<CreateDefaultGroceryCategories.Command, Result<List<GroceryCategory>>>(Result.Ok(TestData.DefaultGroceryCategories()));

        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Single(Sent<CreateDefaultGroceryCategories.Command>());
        Assert.Equal(["produce", "bakery", "dairy_eggs", "other"], SectionKeys(cut));
    }

    [Fact]
    public void CategoriesFailToLoad_ShowsTheErrorAndOneListWithoutHeadings()
    {
        SetupSend<GetGroceryCategories.Query, Result<List<GroceryCategory>>>(Result.Fail<List<GroceryCategory>>("No response from server"));

        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Empty(cut.FindAll(TestId("grocery-category")));
        var names = cut.FindAll($"{TestId("grocery-to-buy")} {TestId("grocery-item-name")}").Select(n => n.TextContent.Trim());
        Assert.Equal(["Milk", "Apples", "mælk", "Flimflam", "Bread"], names);
        Assert.Contains("No response from server", SnackbarMessages);
    }

    [Fact]
    public void TappingAHeading_FoldsTheSection_KeepingItsHeadingAndCount_AndSavesIt()
    {
        var cut = RenderWithPopovers<GroceryListPage>();
        var heading = Section(cut, "dairy_eggs").QuerySelector(TestId("grocery-category-heading"))!;
        Assert.Equal("true", heading.GetAttribute("aria-expanded"));

        heading.Click();

        var dairy = Section(cut, "dairy_eggs");
        Assert.Equal("true", dairy.GetAttribute("data-folded"));
        Assert.Equal("false", dairy.QuerySelector(TestId("grocery-category-heading"))!.GetAttribute("aria-expanded"));
        Assert.Empty(dairy.QuerySelectorAll(TestId("grocery-item")));
        Assert.Equal("Dairy & eggs", dairy.QuerySelector(TestId("grocery-category-name"))!.TextContent.Trim());
        Assert.Equal("2", dairy.QuerySelector(TestId("grocery-category-count"))!.TextContent.Trim());
        Assert.Equal(["Apples"], NamesIn(Section(cut, "produce")));
        LocalStorage.Verify(l => l.SetItemAsync(GroceryCategoriesStateContainer.FoldedStorageKey,
            It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 5 })), It.IsAny<CancellationToken>()));

        Section(cut, "dairy_eggs").QuerySelector(TestId("grocery-category-heading"))!.Click();

        Assert.Equal(["Milk", "mælk"], NamesIn(Section(cut, "dairy_eggs")));
    }

    [Fact]
    public void AFoldedSection_IsRememberedOnThisDevice()
    {
        // Dairy & eggs has id 5 in the default test categories.
        LocalStorage.Setup(l => l.GetItemAsync<List<int>>(GroceryCategoriesStateContainer.FoldedStorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([5]);

        var cut = RenderWithPopovers<GroceryListPage>();

        var dairy = Section(cut, "dairy_eggs");
        Assert.Equal("true", dairy.GetAttribute("data-folded"));
        Assert.Empty(dairy.QuerySelectorAll(TestId("grocery-item")));
        Assert.Equal("2", dairy.QuerySelector(TestId("grocery-category-count"))!.TextContent.Trim());
    }
}
