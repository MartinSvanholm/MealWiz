using Bunit;
using MealWiz.Shared.Components.Drawers;
using Moq;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.Components;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>The drawer that lists items from meals no longer planned, grouped by meal, so the user can remove or keep them.</summary>
public class ReviewGroceryItemsDrawerTests : ComponentTestBase
{
    private readonly GroceryItem _tortillas = TestData.LinkedGroceryItem("Tortillas", 1, "8");
    private readonly GroceryItem _salsa = TestData.LinkedGroceryItem("Salsa", 2);
    private readonly GroceryItem _leeks = TestData.LinkedGroceryItem("Leeks", 3);
    private readonly List<string> _order = [];
    private int _afterRemoveCalls;

    public ReviewGroceryItemsDrawerTests()
    {
        GroceryList.GroceryList.Items.AddRange([_tortillas, _salsa, _leeks]);
        GroceryList.Sources.AddRange([
            TestData.StaleSource(1, 1, "Tacos"), TestData.StaleSource(2, 2, "Tacos"), TestData.StaleSource(3, 3, "Soup")]);
        SetupUnlink(Result.Ok());
        SetupDelete(Result.Ok());
    }

    private List<GroceryItemGroup> Groups() =>
        StaleGroceryItems.GroupByMeal(GroceryList.StaleItems, GroceryList.Sources);

    private void SetupUnlink(Result result) =>
        Mediator.Setup(m => m.Send(It.IsAny<UnlinkGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("unlink")).ReturnsAsync(result);

    private void SetupDelete(Result result) =>
        Mediator.Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("delete")).ReturnsAsync(result);

    private IRenderedComponent<DrawerBase> OpenDrawer(IReadOnlyList<GroceryItemGroup>? groups = null)
    {
        RenderDrawers();
        var open = groups ?? Groups();
        DrawerHost.InvokeAsync(() => ReviewGroceryItemsDrawer.Open(Drawer, "Review items", open, () => { _afterRemoveCalls++; return Task.CompletedTask; }));
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("drawer-title"))));
        return DrawerHost;
    }

    private static string ConfirmText(IRenderedComponent<DrawerBase> host) => host.Find(TestId("review-items-confirm")).TextContent.Trim();

    private static void Toggle(IRenderedComponent<DrawerBase> host, string name) =>
        host.FindAll(TestId("review-item")).Single(li => li.QuerySelector(TestId("review-item-name"))!.TextContent == name)
            .QuerySelector(TestId("review-item-checkbox"))!.Click();

    [Fact]
    public void Opens_with_the_given_title()
    {
        var host = OpenDrawer();

        Assert.Equal("Review items", host.Find(TestId("drawer-title")).TextContent);
        Assert.Equal("Ticked items are removed from your list. Unticked items stay as your own items.", host.Find(TestId("review-items-hint")).TextContent.Trim());
    }

    [Fact]
    public void Lists_each_group_under_its_heading_with_every_item_ticked()
    {
        var host = OpenDrawer();

        var groups = host.FindAll(TestId("review-group"));
        Assert.Equal(["Soup", "Tacos"], groups.Select(g => g.QuerySelector(TestId("review-group-heading"))!.TextContent.Trim()));
        Assert.Equal(["Salsa", "Tortillas"], groups[1].QuerySelectorAll(TestId("review-item-name")).Select(n => n.TextContent));
        Assert.All(host.FindAll(TestId("review-item-checkbox")), c => Assert.Equal("true", c.GetAttribute("aria-checked")));
    }

    [Fact]
    public void Shows_an_items_amount_next_to_its_name()
    {
        var host = OpenDrawer();

        var tortillas = host.FindAll(TestId("review-item")).Single(li => li.TextContent.Contains("Tortillas"));
        Assert.Equal("8", tortillas.QuerySelector(TestId("review-item-amount"))!.TextContent.Trim());
        var salsa = host.FindAll(TestId("review-item")).Single(li => li.TextContent.Contains("Salsa"));
        Assert.Null(salsa.QuerySelector(TestId("review-item-amount")));
    }

    [Fact]
    public void The_button_counts_the_ticked_items()
    {
        var host = OpenDrawer();
        Assert.Equal("Remove 3 items", ConfirmText(host));

        Toggle(host, "Salsa");
        Assert.Equal("Remove 2 items", ConfirmText(host));
        Toggle(host, "Leeks");
        Assert.Equal("Remove 1 item", ConfirmText(host));
        Toggle(host, "Tortillas");
        Assert.Equal("Keep all", ConfirmText(host));
        Toggle(host, "Salsa");
        Assert.Equal("Remove 1 item", ConfirmText(host));
    }

    [Fact]
    public void Remove_unlinks_the_unticked_items_then_deletes_the_ticked_ones()
    {
        var host = OpenDrawer();
        Toggle(host, "Salsa");

        host.Find(TestId("review-items-confirm")).Click();

        host.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal(["unlink", "delete"], _order);
        Assert.Equal([2], Sent<UnlinkGroceryItems.Command>().Single().GroceryItemIds);
        Assert.Equal([1, 3], Sent<DeleteGroceryItems.Command>().Single().GroceryItemIds.Order());
        Assert.Contains("Removed 2 items", SnackbarMessages);
        Assert.Equal(["Salsa"], GroceryList.GroceryList.Items.Select(i => i.Name));
    }

    [Fact]
    public void Keep_all_unlinks_every_item_and_deletes_nothing()
    {
        var host = OpenDrawer();
        foreach (var name in new[] { "Tortillas", "Salsa", "Leeks" }) Toggle(host, name);

        host.Find(TestId("review-items-confirm")).Click();

        host.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal([1, 2, 3], Sent<UnlinkGroceryItems.Command>().Single().GroceryItemIds.Order());
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.Contains("Kept 3 items", SnackbarMessages);
    }

    [Fact]
    public void Remove_runs_AfterRemove_once_after_the_requests()
    {
        var host = OpenDrawer();

        host.Find(TestId("review-items-confirm")).Click();

        host.WaitForAssertion(() => Assert.Equal(1, _afterRemoveCalls));
        Assert.Equal(["delete"], _order);
    }

    [Fact]
    public void A_failed_unlink_shows_the_error_keeps_the_drawer_open_and_skips_AfterRemove()
    {
        SetupUnlink(Result.Fail("Unlink failed"));
        var host = OpenDrawer();
        Toggle(host, "Salsa");

        host.Find(TestId("review-items-confirm")).Click();

        host.WaitForAssertion(() => Assert.Contains("Unlink failed", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal(0, _afterRemoveCalls);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
    }

    [Fact]
    public void A_failed_delete_shows_the_error_keeps_the_drawer_open_and_skips_AfterRemove()
    {
        SetupDelete(Result.Fail("Delete failed"));
        var host = OpenDrawer();

        host.Find(TestId("review-items-confirm")).Click();

        host.WaitForAssertion(() => Assert.Contains("Delete failed", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal(0, _afterRemoveCalls);
    }

    [Fact]
    public void The_button_is_disabled_while_removing()
    {
        var gate = new TaskCompletionSource<Result>();
        Mediator.Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>())).Returns(gate.Task.ContinueWith(t => t.Result));
        var host = OpenDrawer();

        host.Find(TestId("review-items-confirm")).Click();

        host.WaitForAssertion(() => Assert.True(host.Find(TestId("review-items-confirm")).HasAttribute("disabled")));
        gate.SetResult(Result.Ok());
        host.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
    }

    [Fact]
    public void Without_items_says_nothing_to_review()
    {
        var host = OpenDrawer([]);

        Assert.Equal("Nothing to review", host.Find(TestId("review-items-empty")).TextContent.Trim());
        Assert.Empty(host.FindAll(TestId("review-items-confirm")));
    }
}
