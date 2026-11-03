using System.Globalization;
using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.Components;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>The grocery list page: the header and progress, the to-buy list, the basket, adding, editing and deleting.</summary>
public class GroceryListPageTests : ComponentTestBase
{
    private readonly GroceryItem _milk = TestData.GroceryItem("Milk", "1 l", id: 1);
    private readonly GroceryItem _bread = TestData.GroceryItem("Bread", id: 2);

    public GroceryListPageTests()
    {
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem> { _milk, _bread }));
        // AddGroceryItem returns the saved row: the item with its new id
        Mediator
            .Setup(m => m.Send(It.IsAny<AddGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AddGroceryItem.Command c, CancellationToken _) =>
                Result.Ok(new GroceryItem { Id = 99, Name = c.GroceryItem.Name, Amount = c.GroceryItem.Amount }));
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
        SetupSend<DeleteGroceryItem.Command, Result<int>>(Result.Ok(1));
    }

    private static List<string> ToBuy(IRenderedComponent<GroceryListPage> cut) =>
        cut.FindAll($"{TestId("grocery-to-buy")} {TestId("grocery-item-name")}").Select(r => r.TextContent.Trim()).ToList();

    private static List<string> Basket(IRenderedComponent<GroceryListPage> cut) =>
        cut.FindAll($"{TestId("grocery-basket")} {TestId("grocery-item")}").Select(r => r.TextContent.Trim()).ToList();

    private static AngleSharp.Dom.IElement CheckboxOf(IRenderedComponent<GroceryListPage> cut, string name) =>
        cut.FindAll(TestId("grocery-item")).Single(r => r.TextContent.Contains(name)).QuerySelector(TestId("grocery-item-checkbox"))!;

    [Fact]
    public void Loads_and_lists_items_to_buy_with_their_amounts()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Single(Sent<GetAllGroceryItems.Query>());
        Assert.Equal(["Milk", "Bread"], ToBuy(cut));
        var amounts = cut.FindAll(TestId("grocery-item-amount")).Select(a => a.TextContent.Trim()).ToList();
        Assert.Equal(["1 l"], amounts);
        Assert.Contains("To buy · 2", cut.Find(TestId("grocery-to-buy-heading")).TextContent);
        Assert.Empty(cut.FindAll(TestId("grocery-basket")));
    }

    [Fact]
    public void Header_shows_the_item_count_and_week_and_progress_counts_picked_items()
    {
        _bread.IsPicked = true;
        var cut = RenderWithPopovers<GroceryListPage>();

        var week = ISOWeek.GetWeekOfYear(MealPlan.SelectedWeekStart);
        Assert.Equal($"2 items · Week {week}", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.Equal("Groceries", cut.Find(TestId("screen-header-title")).TextContent.Trim());
        Assert.Equal("1 of 2 items", cut.Find(TestId("grocery-progress-count")).TextContent.Trim());
        Assert.Equal("50", cut.Find(TestId("grocery-progress")).GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Checking_off_an_item_saves_it_as_picked_and_moves_it_to_the_basket()
    {
        var cut = RenderWithPopovers<GroceryListPage>();
        Assert.Equal("false", CheckboxOf(cut, "Milk").GetAttribute("aria-checked"));

        CheckboxOf(cut, "Milk").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Bread"], ToBuy(cut)));
        Assert.Equal(["Milk"], Basket(cut));
        Assert.Equal("true", CheckboxOf(cut, "Milk").GetAttribute("aria-checked"));
        Assert.Contains("In the basket · 1", cut.Find(TestId("grocery-basket-heading")).TextContent);
        var command = Assert.Single(Sent<UpdateGroceryItem.Command>());
        Assert.Same(_milk, command.GroceryItem);
        Assert.True(_milk.IsPicked);
    }

    [Fact]
    public void Tapping_an_item_in_the_basket_moves_it_back_to_buy()
    {
        _milk.IsPicked = true;
        var cut = RenderWithPopovers<GroceryListPage>();
        Assert.Equal(["Milk"], Basket(cut));

        CheckboxOf(cut, "Milk").Click();

        cut.WaitForAssertion(() => Assert.Contains("Milk", ToBuy(cut)));
        Assert.False(_milk.IsPicked);
        Assert.Empty(cut.FindAll(TestId("grocery-basket")));
    }

    [Fact]
    public void A_failed_check_off_is_rolled_back()
    {
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Fail<int>("Offline"));
        var cut = RenderWithPopovers<GroceryListPage>();

        CheckboxOf(cut, "Milk").Click();

        cut.WaitForAssertion(() => Assert.Contains("Offline", SnackbarMessages));
        Assert.False(_milk.IsPicked);
        Assert.Equal("Milk", ToBuy(cut)[0]);
    }

    [Fact]
    public void The_add_field_opens_the_add_drawer_with_the_typed_name_and_saves_the_item()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("add-grocery-item-input")).Input("Eggs");
        cut.Find(TestId("add-grocery-item-button")).Click();

        Assert.Equal("Add item", Drawer.StateParameters.Title);
        Assert.Equal("Eggs", DrawerHost.Find(InputOf("grocery-item-name-input")).GetAttribute("value"));
        Assert.Equal("", cut.Find(TestId("add-grocery-item-input")).GetAttribute("value") ?? "");
        DrawerHost.Find(InputOf("grocery-item-amount-input")).Input("12");
        DrawerHost.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Equal(3, ToBuy(cut).Count));
        var added = Assert.Single(Sent<AddGroceryItem.Command>()).GroceryItem;
        Assert.Equal(("Eggs", "12"), (added.Name, added.Amount));
        Assert.Contains(GroceryList.GroceryList.Items, i => i is { Id: 99, Name: "Eggs" });
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void Enter_in_the_add_field_opens_the_add_drawer()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("add-grocery-item-input")).Input("Eggs");
        PressEnter(cut.Find(TestId("add-grocery-item-input")));

        Assert.True(Drawer.IsDrawerOpen);
        var item = Assert.IsType<GroceryItem>(Drawer.StateParameters.ContentParameters[nameof(GroceryItemDrawer.GroceryItem)]);
        Assert.Equal(("Eggs", 0), (item.Name, item.Id));
    }

    [Fact]
    public void Tapping_an_items_name_opens_the_edit_drawer_and_saves_the_change()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(TestId("grocery-item-name"))[0].Click();
        Assert.Equal("Edit item", Drawer.StateParameters.Title);
        DrawerHost.Find(InputOf("grocery-item-amount-input")).Input("2 l");
        DrawerHost.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Equal("2 l", cut.FindAll(TestId("grocery-item-amount"))[0].TextContent.Trim()));
        // The drawer saves a copy of the item (MEA-76)
        var sent = Assert.Single(Sent<UpdateGroceryItem.Command>()).GroceryItem;
        Assert.Equal((_milk.Id, "2 l"), (sent.Id, sent.Amount));
        Assert.Empty(Sent<AddGroceryItem.Command>());
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void Deleting_from_the_edit_drawers_header_after_confirmation_removes_the_item()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(TestId("grocery-item-name"))[0].Click();
        DrawerHost.Find($"{TestId("drawer-header-action")} {TestId("delete-grocery-item-button")}").Click();
        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Equal("Delete Milk?", ConfirmDrawerHost.Find(TestId("confirm-drawer-title")).TextContent.Trim());
        Assert.Empty(Sent<DeleteGroceryItem.Command>());
        Confirm();

        cut.WaitForAssertion(() => Assert.Equal(["Bread"], ToBuy(cut)));
        Assert.Same(_milk, Assert.Single(Sent<DeleteGroceryItem.Command>()).GroceryItem);
        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.False(Drawer.IsDrawerOpen);
    }

    // Clear has no behaviour yet (MEA-73): it's shown as designed but does nothing.
    [Fact]
    public void Clear_in_the_basket_is_shown_but_not_wired()
    {
        _milk.IsPicked = true;
        var cut = RenderWithPopovers<GroceryListPage>();

        var clear = cut.Find(TestId("clear-basket-button"));
        Assert.Equal("Clear", clear.TextContent.Trim());
        Assert.Throws<MissingEventHandlerException>(() => clear.Click());

        Assert.Empty(Sent<DeleteGroceryItem.Command>());
        Assert.Equal(["Milk"], Basket(cut));
    }

    [Fact]
    public void An_empty_list_shows_the_empty_state()
    {
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(new List<GroceryItem>()));
        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.NotNull(cut.Find(TestId("grocery-list-empty")));
        Assert.Equal("0 of 0 items", cut.Find(TestId("grocery-progress-count")).TextContent.Trim());
    }
}
