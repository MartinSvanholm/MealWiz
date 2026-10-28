using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>Adding, checking off, editing and deleting grocery items from the grocery list page.</summary>
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

    private List<string> Rows(IRenderedComponent<GroceryListPage> cut) =>
        cut.FindAll(TestId("grocery-item")).Select(r => r.TextContent.Trim()).ToList();

    [Fact]
    public void Loads_and_lists_items_with_their_amounts()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Single(Sent<GetAllGroceryItems.Query>());
        var rows = Rows(cut);
        Assert.Equal(2, rows.Count);
        Assert.Contains("Milk (1 l)", rows[0]);
        Assert.Equal("Bread", rows[1]);
    }

    [Fact]
    public void Checking_off_an_item_saves_it_as_picked_and_moves_it_last()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(InputOf("grocery-item-checkbox"))[0].Change(true);

        cut.WaitForAssertion(() => Assert.Equal("Bread", Rows(cut)[0]));
        var command = Assert.Single(Sent<UpdateGroceryItem.Command>());
        Assert.Same(_milk, command.GroceryItem);
        Assert.True(_milk.IsPicked);
    }

    [Fact]
    public void A_failed_check_off_is_rolled_back()
    {
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Fail<int>("Offline"));
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(InputOf("grocery-item-checkbox"))[0].Change(true);

        cut.WaitForAssertion(() => Assert.Contains("Offline", SnackbarMessages));
        Assert.False(_milk.IsPicked);
        Assert.Contains("Milk", Rows(cut)[0]);
    }

    [Fact]
    public void Adding_an_item_through_the_drawer_saves_it_and_lists_it()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("add-grocery-item-button")).Click();
        Assert.Equal("Add item", Drawer.StateParameters.Title);
        DrawerHost.Find(InputOf("grocery-item-name-input")).Change("Eggs");
        DrawerHost.Find(InputOf("grocery-item-amount-input")).Input("12");
        DrawerHost.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Equal(3, Rows(cut).Count));
        var added = Assert.Single(Sent<AddGroceryItem.Command>()).GroceryItem;
        Assert.Equal(("Eggs", "12"), (added.Name, added.Amount));
        Assert.Contains(GroceryList.GroceryList.Items, i => i is { Id: 99, Name: "Eggs" });
        Assert.Contains(Rows(cut), r => r.Contains("Eggs (12)"));
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void Editing_an_item_through_the_drawer_updates_it()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(TestId("edit-grocery-item-button"))[0].Click();
        Assert.Equal("Edit item", Drawer.StateParameters.Title);
        DrawerHost.Find(InputOf("grocery-item-amount-input")).Input("2 l");
        DrawerHost.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Milk (2 l)", Rows(cut)[0]));
        Assert.Same(_milk, Assert.Single(Sent<UpdateGroceryItem.Command>()).GroceryItem);
        Assert.Empty(Sent<AddGroceryItem.Command>());
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void Deleting_an_item_after_confirmation_removes_it()
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.FindAll(TestId("delete-grocery-item-button"))[0].Click();
        Assert.True(Drawer.IsConfirmDrawerOpen);
        Confirm();

        cut.WaitForAssertion(() => Assert.Equal(["Bread"], Rows(cut)));
        Assert.Same(_milk, Assert.Single(Sent<DeleteGroceryItem.Command>()).GroceryItem);
        Assert.False(Drawer.IsConfirmDrawerOpen);
    }

    [Fact]
    public void Account_button_opens_the_account_page()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("account-button")).Click();

        Assert.Equal("account", CurrentPath);
    }
}
