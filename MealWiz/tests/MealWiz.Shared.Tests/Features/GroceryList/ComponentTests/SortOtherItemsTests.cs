using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.RememberGroceryName;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>Sort on the Other heading: the Other items one at a time, each placed with a chip or skipped.</summary>
public class SortOtherItemsTests : ComponentTestBase
{
    public SortOtherItemsTests()
    {
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>
        {
            TestData.GroceryItem("Flimflam", id: 1),
            TestData.GroceryItem("Milk", id: 2),
            TestData.GroceryItem("Zorbex", id: 3),
            TestData.GroceryItem("flimflam", id: 4),
            TestData.GroceryItem("Quux", id: 5),
        }));
        SetupSend<RememberGroceryName.Command, Result>(Result.Ok());
    }

    private IRenderedComponent<GroceryListPage> RenderAndOpenSort()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();
        cut.Find(TestId("grocery-sort-other")).Click();
        return cut;
    }

    private string SortedName => DrawerHost.Find(TestId("sort-item-name")).TextContent.Trim();

    private void Tap(string key) =>
        DrawerHost.FindAll(TestId("category-chip")).Single(c => c.GetAttribute("data-category") == key).Click();

    private static List<string?> SectionKeys(IRenderedComponent<GroceryListPage> cut) =>
        cut.FindAll(TestId("grocery-category")).Select(s => s.GetAttribute("data-category")).ToList();

    [Fact]
    public void Sort_IsOnlyOnTheOtherHeading_AndDoesNotFoldIt()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        var sort = Assert.Single(cut.FindAll(TestId("grocery-sort-other")));
        Assert.NotNull(sort.Closest("[data-category=other]"));
        Assert.Null(sort.Closest(TestId("grocery-category-heading")));
    }

    [Fact]
    public void Sort_ShowsTheFirstOtherItem_WithTheChipsButNotOther()
    {
        RenderAndOpenSort();

        Assert.Equal("Flimflam", SortedName);
        Assert.Equal("1 of 4", DrawerHost.Find(TestId("sort-progress")).TextContent.Trim());
        Assert.Equal(10, DrawerHost.FindAll(TestId("category-chip")).Count);
        Assert.Empty(DrawerHost.FindAll("[data-testid=category-chip][data-category=other]"));
    }

    [Fact]
    public void PickingAChip_MovesTheName_SkipsItsOtherItems_AndShowsTheNext()
    {
        var cut = RenderAndOpenSort();

        Tap("pantry");

        var command = Assert.Single(Sent<RememberGroceryName.Command>());
        Assert.Equal(("Flimflam", 7), (command.Name, command.CategoryId));
        // The second "flimflam" moved with the first, so Zorbex is next.
        DrawerHost.WaitForAssertion(() => Assert.Equal("Zorbex", SortedName));
        cut.WaitForAssertion(() => Assert.Contains("pantry", SectionKeys(cut)));
        var pantry = cut.Find("[data-testid=grocery-category][data-category=pantry]");
        Assert.Equal("2", pantry.QuerySelector(TestId("grocery-category-count"))!.TextContent.Trim());
    }

    [Fact]
    public void Skip_LeavesTheItemInOther_AndTheDrawerClosesAfterTheLast()
    {
        RenderAndOpenSort();

        DrawerHost.Find(TestId("sort-skip")).Click();
        Assert.Equal("Zorbex", SortedName);
        DrawerHost.Find(TestId("sort-skip")).Click();
        Assert.Equal("flimflam", SortedName);
        DrawerHost.Find(TestId("sort-skip")).Click();
        Assert.Equal("Quux", SortedName);
        Tap("frozen");

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal("Quux", Assert.Single(Sent<RememberGroceryName.Command>()).Name);
    }

    [Fact]
    public void AFailedMove_ShowsTheError_AndStaysOnTheItem()
    {
        SetupSend<RememberGroceryName.Command, Result>(Result.Fail("No response from server"));
        RenderAndOpenSort();

        Tap("pantry");

        DrawerHost.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.Equal("Flimflam", SortedName);
        Assert.True(Drawer.IsDrawerOpen);
    }
}
