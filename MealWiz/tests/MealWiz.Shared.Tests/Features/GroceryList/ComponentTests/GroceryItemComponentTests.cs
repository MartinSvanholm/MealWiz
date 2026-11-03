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

/// <summary>The grocery item drawer, the add field and the edit/delete buttons, each on its own.</summary>
public class GroceryItemComponentTests : ComponentTestBase
{
    [Fact]
    public void Drawer_shows_the_item_being_edited()
    {
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, TestData.GroceryItem("Milk", "1 l", id: 1)));

        Assert.Equal("Milk", cut.Find(InputOf("grocery-item-name-input")).GetAttribute("value"));
        Assert.Equal("1 l", cut.Find(InputOf("grocery-item-amount-input")).GetAttribute("value"));
    }

    // MEA-76: the drawer used to edit the listed item itself, so closing it without saving left the change showing.
    [Fact]
    public void Editing_in_the_drawer_leaves_the_listed_item_alone_until_saved()
    {
        SetupSend<UpdateGroceryItem.Command, Result<int>>(Result.Ok(1));
        var milk = TestData.GroceryItem("Milk", "1 l", id: 1);
        GroceryList.GroceryList.Items.Add(milk);
        Drawer.OpenDrawer("Edit item", typeof(GroceryItemDrawer));
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, milk));

        cut.Find(InputOf("grocery-item-name-input")).Change("Oat milk");
        cut.Find(InputOf("grocery-item-amount-input")).Input("2 l");

        Assert.Equal(("Milk", "1 l"), (milk.Name, milk.Amount));

        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        var listed = Assert.Single(GroceryList.GroceryList.Items);
        Assert.Equal((1, "Oat milk", "2 l"), (listed.Id, listed.Name, listed.Amount));
    }

    [Fact]
    public void Enter_in_the_drawers_name_moves_to_the_amount()
    {
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, TestData.GroceryItem("Milk")));

        PressEnter(cut.Find(InputOf("grocery-item-name-input")));

        AssertFocused(cut.Find(InputOf("grocery-item-amount-input")));
    }

    [Fact]
    public void Enter_in_the_drawers_amount_saves_the_item()
    {
        SetupSend<AddGroceryItem.Command, Result<GroceryItem>>(Result.Ok(TestData.GroceryItem("Eggs", "6", id: 5)));
        Drawer.OpenDrawer("Add item", typeof(GroceryItemDrawer));
        var item = TestData.GroceryItem("Eggs");
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, item));

        cut.Find(InputOf("grocery-item-amount-input")).Input("6");
        PressEnter(cut.Find(InputOf("grocery-item-amount-input")));

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal("6", Assert.Single(Sent<AddGroceryItem.Command>()).GroceryItem.Amount);
    }

    [Fact]
    public void Drawer_with_a_name_already_filled_in_focuses_the_amount()
    {
        var cut = Render<GroceryItemDrawer>(p => p.Add(d => d.GroceryItem, TestData.GroceryItem("Eggs")));

        AssertFocused(cut.Find(InputOf("grocery-item-amount-input")));
    }

    [Fact]
    public void Saving_a_new_item_adds_it_and_closes_the_drawer()
    {
        var saved = TestData.GroceryItem("Eggs", id: 5);
        SetupSend<AddGroceryItem.Command, Result<GroceryItem>>(Result.Ok(saved));
        Drawer.OpenDrawer("Add item", typeof(GroceryItemDrawer));
        var item = TestData.GroceryItem("Eggs");
        var cut = Render<SaveGroceryItemButton>(p => p.Add(b => b.GroceryItem, item));

        cut.Find(TestId("save-grocery-item-button")).Click();

        cut.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Same(item, Assert.Single(Sent<AddGroceryItem.Command>()).GroceryItem);
        Assert.Contains(saved, GroceryList.GroceryList.Items);
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
    public void Add_field_opens_the_drawer_with_a_new_item_named_as_typed()
    {
        var cut = RenderWithPopovers<AddGroceryItemField>();

        cut.Find(TestId("add-grocery-item-input")).Input("Eggs");
        cut.Find(TestId("add-grocery-item-button")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Add item", Drawer.StateParameters.Title);
        Assert.Equal(typeof(GroceryItemDrawer), Drawer.StateParameters.Content);
        var item = Assert.IsType<GroceryItem>(Drawer.StateParameters.ContentParameters[nameof(GroceryItemDrawer.GroceryItem)]);
        Assert.Equal((0, "Eggs"), (item.Id, item.Name));
        Assert.Null(Drawer.StateParameters.HeaderAction);
    }

    [Fact]
    public void Add_field_left_empty_opens_an_empty_drawer()
    {
        var cut = RenderWithPopovers<AddGroceryItemField>();

        cut.Find(TestId("add-grocery-item-button")).Click();

        var item = Assert.IsType<GroceryItem>(Drawer.StateParameters.ContentParameters[nameof(GroceryItemDrawer.GroceryItem)]);
        Assert.Equal("", item.Name);
    }

    [Fact]
    public void Tapping_the_name_opens_the_edit_drawer_with_that_item_and_a_delete_action()
    {
        var milk = TestData.GroceryItem("Milk", id: 1);
        RenderDrawers();
        var cut = RenderWithPopovers<EditGroceryItemButton>(p => p.Add(b => b.GroceryItem, milk));

        Assert.Equal("Milk", cut.Find(TestId("grocery-item-name")).TextContent.Trim());
        cut.Find(TestId("grocery-item-name")).Click();

        Assert.Equal("Edit item", Drawer.StateParameters.Title);
        Assert.Same(milk, Drawer.StateParameters.ContentParameters[nameof(GroceryItemDrawer.GroceryItem)]);
        DrawerHost.WaitForAssertion(() =>
            Assert.NotNull(DrawerHost.Find($"{TestId("drawer-header-action")} {TestId("delete-grocery-item-button")}")));
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

        Assert.Equal("Delete Milk?", ConfirmDrawerHost.Find(TestId("confirm-drawer-title")).TextContent.Trim());
        Assert.Contains("Are you sure you want to delete this grocery item?", ConfirmDrawerHost.Find(TestId("confirm-drawer-description")).TextContent);
        Assert.Empty(Sent<DeleteGroceryItem.Command>());

        Confirm();

        cut.WaitForAssertion(() => Assert.Empty(GroceryList.GroceryList.Items));
        Assert.Single(Sent<DeleteGroceryItem.Command>());
    }

    // MEA-32: a dismissed confirmation used to leave its handler subscribed, so the next confirm deleted it too.
    [Fact]
    public void Dismissing_the_confirmation_then_confirming_another_deletes_only_the_confirmed_item()
    {
        SetupSend<DeleteGroceryItem.Command, Result<int>>(Result.Ok(1));
        var milk = TestData.GroceryItem("Milk", id: 1);
        var bread = TestData.GroceryItem("Bread", id: 2);
        GroceryList.GroceryList.Items.AddRange([milk, bread]);
        RenderDrawers();
        var milkButton = Render<DeleteGroceryItemButton>(p => p.Add(b => b.GroceryItem, milk));
        var breadButton = Render<DeleteGroceryItemButton>(p => p.Add(b => b.GroceryItem, bread));

        milkButton.Find(TestId("delete-grocery-item-button")).Click();
        ConfirmDrawerHost.InvokeAsync(() => Drawer.IsConfirmDrawerOpen = false);
        breadButton.Find(TestId("delete-grocery-item-button")).Click();
        Confirm();

        breadButton.WaitForAssertion(() => Assert.DoesNotContain(bread, GroceryList.GroceryList.Items));
        Assert.Single(Sent<DeleteGroceryItem.Command>());
        Assert.Contains(milk, GroceryList.GroceryList.Items);
    }
}
