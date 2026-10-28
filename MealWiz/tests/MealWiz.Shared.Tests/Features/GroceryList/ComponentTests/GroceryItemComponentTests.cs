using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.Components;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.ComponentTests;

/// <summary>The grocery item drawer and the add/edit/delete buttons, each on its own.</summary>
public class GroceryItemComponentTests : ComponentTestBase
{
    [Fact]
    public void Drawer_shows_the_item_being_edited()
    {
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, TestData.GroceryItem("Milk", "1 l", id: 1)));

        Assert.Equal("Milk", cut.Find(InputOf("grocery-item-name-input")).GetAttribute("value"));
        Assert.Equal("1 l", cut.Find(InputOf("grocery-item-amount-input")).GetAttribute("value"));
    }

    [Fact]
    public void Saving_a_new_item_adds_it_and_closes_the_drawer()
    {
        SetupSend<AddGroceryItem.Command, Result<int>>(Result.Ok(5));
        Drawer.OpenDrawer("Add item", typeof(GroceryItemDrawer));
        var item = TestData.GroceryItem("Eggs");
        var cut = Render<SaveGroceryItemButton>(p => p.Add(b => b.GroceryItem, item));

        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Same(item, Assert.Single(Sent<AddGroceryItem.Command>()).GroceryItem);
        Assert.Contains(item, GroceryList.GroceryList.Items);
        Assert.Empty(Sent<UpdateGroceryItem.Command>());
    }

    [Fact]
    public void Saving_an_existing_item_updates_it_and_closes_the_drawer()
    {
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
        Drawer.OpenDrawer("Edit item", typeof(GroceryItemDrawer));
        var item = TestData.GroceryItem("Milk", id: 1);
        var cut = Render<SaveGroceryItemButton>(p => p.Add(b => b.GroceryItem, item));

        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Same(item, Assert.Single(Sent<UpdateGroceryItem.Command>()).GroceryItem);
        Assert.Empty(Sent<AddGroceryItem.Command>());
    }

    [Fact]
    public void Add_button_opens_the_drawer_with_a_new_item()
    {
        var cut = RenderWithPopovers<AddGroceryItemButton>();

        cut.Find(TestId("add-grocery-item-button")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Add item", Drawer.StateParameters.Title);
        Assert.Equal(typeof(GroceryItemDrawer), Drawer.StateParameters.Content);
        var item = Assert.IsType<GroceryItem>(Drawer.StateParameters.ContentParameters[nameof(GroceryItemDrawer.GroceryItem)]);
        Assert.Equal(0, item.Id);
    }

    [Fact]
    public void Edit_button_opens_the_drawer_with_that_item()
    {
        var milk = TestData.GroceryItem("Milk", id: 1);
        var cut = RenderWithPopovers<UpdatedGroceryItemButton>(p => p.Add(b => b.GroceryItem, milk));

        cut.Find(TestId("edit-grocery-item-button")).Click();

        Assert.Equal("Edit item", Drawer.StateParameters.Title);
        Assert.Same(milk, Drawer.StateParameters.ContentParameters[nameof(GroceryItemDrawer.GroceryItem)]);
    }

    [Fact]
    public void Delete_button_deletes_only_after_confirmation()
    {
        SetupSend<DeleteGroceryItem.Command, Result<int>>(Result.Ok(1));
        var milk = TestData.GroceryItem("Milk", id: 1);
        GroceryList.GroceryList.Items.Add(milk);
        RenderDrawers();
        var cut = Render<DeleteGroceryItemButton>(p => p.Add(b => b.GroceryItem, milk));

        cut.Find(TestId("delete-grocery-item-button")).Click();

        Assert.Contains("Are you sure you want to delete this grocery item?", ConfirmDrawerHost.Find(TestId("confirm-drawer-description")).TextContent);
        Assert.Empty(Sent<DeleteGroceryItem.Command>());

        Confirm();

        cut.WaitForAssertion(() => Assert.Empty(GroceryList.GroceryList.Items));
        Assert.Single(Sent<DeleteGroceryItem.Command>());
    }
}
