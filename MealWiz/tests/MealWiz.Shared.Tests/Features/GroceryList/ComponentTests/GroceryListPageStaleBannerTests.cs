using Bunit;
using FluentResults;
using MealWiz.Shared.Components.Drawers;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.Components;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>The Groceries banner for items from meals no longer in the plan, and the review drawer it opens.</summary>
public class GroceryListPageStaleBannerTests : ComponentTestBase
{
    private readonly GroceryItem _tortillas = TestData.LinkedGroceryItem("Tortillas", 1, "8");
    private readonly GroceryItem _salsa = TestData.LinkedGroceryItem("Salsa", 2);
    private readonly GroceryItem _milk = TestData.GroceryItem("Milk", id: 3, isManual: true);
    private readonly GroceryItem _leeks = TestData.LinkedGroceryItem("Leeks", 4);
    private List<GroceryItemSource> _sources;

    public GroceryListPageStaleBannerTests()
    {
        _sources =
        [
            TestData.StaleSource(1, 1, "Tacos"), TestData.StaleSource(2, 2, "Tacos"),
            TestData.StaleSource(3, 3, "Old"), TestData.CurrentSource(4, 4)
        ];
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem> { _tortillas, _salsa, _milk, _leeks }));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(_sources));
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
        SetupSend<UnlinkGroceryItems.Command, Result>(Result.Ok());
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
    }

    private void SetSources(params GroceryItemSource[] sources)
    {
        _sources = [.. sources];
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(_sources));
    }

    private IRenderedComponent<GroceryListPage> RenderPage()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(TestId("grocery-item"))));
        return cut;
    }

    private static string BannerText(IRenderedComponent<GroceryListPage> cut) => cut.Find(TestId("stale-items-text")).TextContent.Trim();

    [Fact]
    public void Loads_the_sources_and_shows_how_many_items_are_from_meals_no_longer_planned()
    {
        // Tortillas and Salsa are stale; Milk is manual and Leeks still has a current source
        var cut = RenderPage();

        Assert.Single(Sent<GetGroceryItemSources.Query>());
        Assert.Equal("2 items are from meals no longer in your plan", BannerText(cut));
    }

    [Fact]
    public void Uses_the_singular_for_one_item()
    {
        SetSources(TestData.StaleSource(1, 1, "Tacos"));

        var cut = RenderPage();

        Assert.Equal("1 item is from a meal no longer in your plan", BannerText(cut));
    }

    [Fact]
    public void Hides_the_banner_when_no_item_is_stale()
    {
        SetSources(TestData.CurrentSource(1, 1), TestData.CurrentSource(2, 4));

        var cut = RenderPage();

        Assert.Empty(cut.FindAll(TestId("stale-items-banner")));
    }

    [Fact]
    public void Basket_and_manual_items_never_count()
    {
        _tortillas.IsPicked = true;
        SetSources(TestData.StaleSource(1, 1, "Tacos"), TestData.StaleSource(3, 3, "Old"));

        var cut = RenderPage();

        Assert.Empty(cut.FindAll(TestId("stale-items-banner")));
    }

    [Fact]
    public void Ticking_off_a_stale_item_lowers_the_count()
    {
        var cut = RenderPage();

        cut.FindAll(TestId("grocery-item")).Single(r => r.TextContent.Contains("Tortillas"))
            .QuerySelector(TestId("grocery-item-checkbox"))!.Click();

        cut.WaitForAssertion(() => Assert.Equal("1 item is from a meal no longer in your plan", BannerText(cut)));
    }

    [Fact]
    public void Review_opens_the_review_drawer_grouped_by_old_meal_and_all_ticked()
    {
        var cut = RenderPage();

        cut.Find(TestId("stale-items-review")).Click();

        Assert.Equal("Review items", Drawer.StateParameters.Title);
        var groups = Assert.IsAssignableFrom<IReadOnlyList<GroceryItemGroup>>(Drawer.StateParameters.ContentParameters[nameof(ReviewGroceryItemsDrawer.Groups)]);
        var group = Assert.Single(groups);
        Assert.Equal("Tacos", group.Heading);
        Assert.Equal(["Salsa", "Tortillas"], group.Items.Select(i => i.Name));
        DrawerHost.WaitForAssertion(() =>
            Assert.All(DrawerHost.FindAll(TestId("review-item-checkbox")), c => Assert.Equal("true", c.GetAttribute("aria-checked"))));
    }

    [Fact]
    public void Removing_from_the_review_drawer_hides_the_banner_and_keeps_the_unticked_item()
    {
        var cut = RenderPage();
        cut.Find(TestId("stale-items-review")).Click();
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("review-item-checkbox"))));
        DrawerHost.FindAll(TestId("review-item")).Single(li => li.TextContent.Contains("Salsa"))
            .QuerySelector(TestId("review-item-checkbox"))!.Click();

        DrawerHost.Find(TestId("review-items-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(TestId("stale-items-banner"))));
        var names = cut.FindAll(TestId("grocery-item-name")).Select(n => n.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Tortillas", names);
        Assert.Contains("Salsa", names);
    }
}
