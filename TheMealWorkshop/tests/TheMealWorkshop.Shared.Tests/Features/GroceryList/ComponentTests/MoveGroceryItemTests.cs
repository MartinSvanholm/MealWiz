using AngleSharp.Dom;
using Bunit;
using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.RememberGroceryName;
using TheMealWorkshop.Shared.Features.GroceryList;
using TheMealWorkshop.Shared.Features.GroceryList.GetAllGroceryItems;
using TheMealWorkshop.Shared.Features.GroceryList.Models;
using TheMealWorkshop.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>
/// Swiping a to-buy row right opens Move: the category chips with the item's category pressed. Tapping another chip
/// moves the name (everywhere, like the drawers' chips) and closes the drawer.
/// </summary>
public class MoveGroceryItemTests : ComponentTestBase
{
    public MoveGroceryItemTests()
    {
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>
        {
            TestData.GroceryItem("Milk", "1 l", id: 1),
            TestData.GroceryItem("Bread", id: 2),
        }));
        SetupSend<RememberGroceryName.Command, Result>(Result.Ok());
    }

    // Drags the row of the to-buy item called name by (dx, dy) in two moves, then lets go. Re-finds the row each time
    // because every move re-renders it.
    private static void SwipeRow(IRenderedComponent<GroceryListPage> cut, string name, double dx, double dy = 0, string pointerType = "touch")
    {
        IElement Row() => cut.FindAll(TestId("grocery-item"))
            .Single(r => r.QuerySelector(TestId("grocery-item-name"))!.TextContent.Trim() == name)
            .QuerySelector(TestId("grocery-item-swipe"))!;
        PointerEventArgs At(double x, double y) => new() { PointerId = 1, PointerType = pointerType, IsPrimary = true, ClientX = 200 + x, ClientY = 100 + y };

        Row().PointerDown(At(0, 0));
        Row().PointerMove(At(dx / 2, dy / 2));
        Row().PointerMove(At(dx, dy));
        Row().PointerUp(At(dx, dy));
    }

    private void Tap(string key) =>
        DrawerHost.FindAll(TestId("category-chip")).Single(c => c.GetAttribute("data-category") == key).Click();

    private static List<string> ItemsIn(IRenderedComponent<GroceryListPage> cut, string key) =>
        cut.FindAll($"[data-testid=grocery-category][data-category={key}] [data-testid=grocery-item-name]")
            .Select(n => n.TextContent.Trim()).ToList();

    [Theory]
    [InlineData("touch")]
    [InlineData("mouse")]
    public void Swiping_a_row_right_opens_Move_with_the_items_category_pressed(string pointerType)
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        SwipeRow(cut, "Milk", dx: 120, pointerType: pointerType);

        Assert.True(Drawer.IsDrawerOpen);
        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Equal("Move Milk", Drawer.StateParameters.Title);
        Assert.Contains("Milk", DrawerHost.Find(TestId("move-item-hint")).TextContent);
        var pressed = Assert.Single(DrawerHost.FindAll("[data-testid=category-chip][aria-pressed=true]"));
        Assert.Equal("dairy_eggs", pressed.GetAttribute("data-category"));
        Assert.Empty(Sent<RememberGroceryName.Command>());
    }

    [Fact]
    public void Tapping_another_category_moves_the_name_and_closes_the_drawer()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        SwipeRow(cut, "Milk", dx: 120);
        Tap("pantry");

        var command = Assert.Single(Sent<RememberGroceryName.Command>());
        Assert.Equal(("Milk", 7), (command.Name, command.CategoryId));
        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        cut.WaitForAssertion(() => Assert.Equal(["Milk"], ItemsIn(cut, "pantry")));
        Assert.Empty(cut.FindAll("[data-testid=grocery-category][data-category=dairy_eggs]"));
    }

    [Fact]
    public void Tapping_the_category_it_is_in_just_closes_the_drawer()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        SwipeRow(cut, "Bread", dx: 120);
        Tap("bakery");

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Empty(Sent<RememberGroceryName.Command>());
        Assert.Equal(["Bread"], ItemsIn(cut, "bakery"));
    }

    [Fact]
    public void A_failed_move_shows_the_error_and_keeps_the_drawer_and_the_item()
    {
        SetupSend<RememberGroceryName.Command, Result>(Result.Fail("No response from server"));
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        SwipeRow(cut, "Milk", dx: 120);
        Tap("pantry");

        DrawerHost.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal(["Milk"], ItemsIn(cut, "dairy_eggs"));
        Assert.Empty(ItemsIn(cut, "pantry"));
    }

    [Theory]
    [InlineData(40, 0)]   // too short
    [InlineData(30, 120)] // mostly vertical: the page scrolls
    public void A_short_or_vertical_right_swipe_opens_nothing(double dx, double dy)
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        SwipeRow(cut, "Milk", dx, dy);

        Assert.False(Drawer.IsDrawerOpen);
        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Equal(["Milk"], ItemsIn(cut, "dairy_eggs"));
    }

    [Fact]
    public void The_row_follows_the_finger_to_the_right()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(TestId("grocery-item-swipe"))[0].PointerDown(new PointerEventArgs { PointerId = 1, PointerType = "touch", ClientX = 200, ClientY = 100 });
        cut.FindAll(TestId("grocery-item-swipe"))[0].PointerMove(new PointerEventArgs { PointerId = 1, PointerType = "touch", ClientX = 250, ClientY = 102 });

        Assert.Contains("translateX(50px)", cut.FindAll(TestId("grocery-item-swipe"))[0].GetAttribute("style"));
    }

    [Fact]
    public void Without_categories_a_right_swipe_does_nothing()
    {
        SetupSend<GetGroceryCategories.Query, Result<List<GroceryCategory>>>(Result.Fail("No response from server"));
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        SwipeRow(cut, "Milk", dx: 120);

        Assert.False(Drawer.IsDrawerOpen);
        Assert.DoesNotContain("translateX(", cut.FindAll(TestId("grocery-item-swipe"))[0].GetAttribute("style") ?? "");
    }
}
