using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;
using MealWiz.Shared.Features.Ingredients;
using MealWiz.Shared.Features.Ingredients.DeleteIngredient;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.ComponentTests;

/// <summary>Shared setup for the delete-ingredient flow with grocery items (MEA-93).</summary>
public abstract class DeleteIngredientWithItemsTestBase : ComponentTestBase
{
    protected readonly Ingredient Eggs = TestData.Ingredient("Eggs", "6", id: 11);

    protected static GroceryItemSource LinkedTo(int ingredientId, int id, int itemId) =>
        new() { Id = id, GroceryItemId = itemId, PlannedMealId = 500 + id, IngredientId = ingredientId, IngredientMealId = 42, PlannedDayMealId = 42 };

    /// <summary>Eggs (item 1) is still to buy; with <paramref name="inBasket"/> a picked Eggs item (2) too.</summary>
    protected void SetupLinkedItems(bool toBuy = true, bool inBasket = false)
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 42, ingredients: [Eggs]);
        var items = new List<GroceryItem>();
        var sources = new List<GroceryItemSource>();
        if (toBuy) { items.Add(TestData.LinkedGroceryItem("Eggs", 1)); sources.Add(LinkedTo(11, 21, 1)); }
        if (inBasket) { items.Add(TestData.LinkedGroceryItem("Eggs", 2, isPicked: true)); sources.Add(LinkedTo(11, 22, 2)); }

        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items));
        SetupSend<GetGroceryItemSources.Query, Result<List<GroceryItemSource>>>(Result.Ok(sources));
        SetupSend<DeleteIngredient.Command, Result>(Result.Ok());
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
        SetupSend<UnlinkGroceryItems.Command, Result>(Result.Ok());
        SetupSend<DeleteGroceryItemSources.Command, Result>(Result.Ok());
        SetupSend<GetIngredientsByMealId.Query, Result<List<Ingredient>>>(Result.Ok(new List<Ingredient>()));
        RenderDrawers();
    }
}

public class RemoveIngredientFromGroceryListDrawerTests : DeleteIngredientWithItemsTestBase
{
    private void Open()
    {
        var links = LinkedGroceryItems.ForIngredient(11, [TestData.LinkedGroceryItem("Eggs", 1)], [LinkedTo(11, 21, 1)]);
        var localizer = Services.GetRequiredService<IStringLocalizer<IngredientsResource>>();
        DrawerHost.InvokeAsync(() => RemoveIngredientFromGroceryListDrawer.Open(Drawer, localizer, Eggs, links));
        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("remove-ingredient-items-yes"))));
    }

    [Fact]
    public void Asks_whether_to_remove_the_ingredient_from_the_grocery_list()
    {
        SetupLinkedItems();
        Open();

        Assert.Equal("On your grocery list", Drawer.StateParameters.Title);
        Assert.Equal("Also remove Eggs from the grocery list?", DrawerHost.Find(TestId("remove-ingredient-items-question")).TextContent);
        Assert.Equal("Eggs is deleted from the meal either way.", DrawerHost.Find(TestId("remove-ingredient-items-hint")).TextContent);
        Assert.Equal("No, keep it", DrawerHost.Find(TestId("remove-ingredient-items-no")).TextContent.Trim());
        Assert.Equal("Yes, remove it", DrawerHost.Find(TestId("remove-ingredient-items-yes")).TextContent.Trim());
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void Yes_deletes_the_ingredient_then_its_items()
    {
        SetupLinkedItems();
        Open();

        DrawerHost.Find(TestId("remove-ingredient-items-yes")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Equal(
            [typeof(DeleteIngredient.Command), typeof(DeleteGroceryItems.Command), typeof(GetIngredientsByMealId.Query)],
            SentRequests.Select(r => r.GetType()));
        Assert.Equal([1], Assert.Single(Sent<DeleteGroceryItems.Command>()).GroceryItemIds);
        Assert.Contains("Ingredient deleted and removed from the grocery list.", SnackbarMessages);
    }

    [Fact]
    public void No_deletes_the_ingredient_and_unlinks_its_items()
    {
        SetupLinkedItems();
        Open();

        DrawerHost.Find(TestId("remove-ingredient-items-no")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Single(Sent<DeleteIngredient.Command>());
        Assert.Equal([1], Assert.Single(Sent<UnlinkGroceryItems.Command>()).GroceryItemIds);
        Assert.Empty(Sent<DeleteGroceryItems.Command>());
        Assert.Contains("Ingredient deleted.", SnackbarMessages);
    }

    [Fact]
    public void A_failed_ingredient_delete_keeps_the_drawer_open_and_sends_nothing_else()
    {
        SetupLinkedItems();
        SetupSend<DeleteIngredient.Command, Result>(Result.Fail("Delete broke"));
        Open();

        DrawerHost.Find(TestId("remove-ingredient-items-yes")).Click();

        DrawerHost.WaitForAssertion(() => Assert.Contains("Delete broke", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal([typeof(DeleteIngredient.Command)], SentRequests.Select(r => r.GetType()));
        Assert.False(DrawerHost.Find(TestId("remove-ingredient-items-yes")).HasAttribute("disabled"));
    }

    [Fact]
    public void A_failed_grocery_step_still_closes_and_reloads()
    {
        SetupLinkedItems();
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Fail("Items broke"));
        Open();

        DrawerHost.Find(TestId("remove-ingredient-items-yes")).Click();

        DrawerHost.WaitForAssertion(() => Assert.False(Drawer.IsDrawerOpen));
        Assert.Contains("Items broke", SnackbarMessages);
        Assert.Single(Sent<GetIngredientsByMealId.Query>());
        Assert.DoesNotContain("Ingredient deleted and removed from the grocery list.", SnackbarMessages);
    }
}

/// <summary>The question's texts come from resources (UI-03): every localizer echoes its key as <c>[Key]</c>.</summary>
public class RemoveIngredientFromGroceryListDrawerTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void The_texts_come_from_the_resource()
    {
        RenderDrawers();
        var localizer = Services.GetRequiredService<IStringLocalizer<IngredientsResource>>();

        DrawerHost.InvokeAsync(() => RemoveIngredientFromGroceryListDrawer.Open(Drawer, localizer, TestData.Ingredient("Eggs", id: 11), LinkedGroceryItems.Empty));

        Assert.Equal("[RemoveIngredient_Title]", Drawer.StateParameters.Title);
        DrawerHost.WaitForAssertion(() =>
        {
            Assert.Equal("[RemoveIngredient_Question]", DrawerHost.Find(TestId("remove-ingredient-items-question")).TextContent);
            Assert.Equal("[RemoveIngredient_No]", DrawerHost.Find(TestId("remove-ingredient-items-no")).TextContent.Trim());
            Assert.Equal("[RemoveIngredient_Yes]", DrawerHost.Find(TestId("remove-ingredient-items-yes")).TextContent.Trim());
        });
    }
}

/// <summary>Deleting a saved ingredient with grocery items asks about them (MEA-93).</summary>
public class DeleteIngredientWithGroceryItemsTests : DeleteIngredientWithItemsTestBase
{
    private IRenderedComponent<DeleteIngredientButton> RenderAndConfirm()
    {
        var cut = Render<DeleteIngredientButton>(p => p.Add(b => b.Ingredient, Eggs));
        cut.Find(TestId("delete-ingredient-button")).Click();
        Confirm();
        return cut;
    }

    [Fact]
    public void Confirming_with_items_to_buy_asks_before_deleting()
    {
        SetupLinkedItems();

        RenderAndConfirm();

        DrawerHost.WaitForAssertion(() => Assert.Equal("Also remove Eggs from the grocery list?", DrawerHost.Find(TestId("remove-ingredient-items-question")).TextContent));
        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(Sent<DeleteIngredient.Command>());
    }

    [Fact]
    public void Confirming_with_only_basket_items_deletes_and_unlinks_without_asking()
    {
        SetupLinkedItems(toBuy: false, inBasket: true);

        var cut = RenderAndConfirm();

        cut.WaitForAssertion(() => Assert.Single(Sent<GetIngredientsByMealId.Query>()));
        Assert.Single(Sent<DeleteIngredient.Command>());
        Assert.Equal([2], Assert.Single(Sent<UnlinkGroceryItems.Command>()).GroceryItemIds);
        Assert.False(Drawer.IsDrawerOpen);
        Assert.Contains("Ingredient deleted.", SnackbarMessages);
    }

    [Fact]
    public void A_failed_load_keeps_the_confirmation_open()
    {
        SetupLinkedItems();
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Fail<List<GroceryItem>>("Items broke"));

        var cut = RenderAndConfirm();

        cut.WaitForAssertion(() => Assert.Contains("Items broke", SnackbarMessages));
        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(Sent<DeleteIngredient.Command>());
    }

    [Fact]
    public void Deleting_from_the_drawer_header_with_items_to_buy_shows_the_question_in_the_drawer()
    {
        SetupLinkedItems();
        var cut = Render<IngredientCard>(p => p.Add(c => c.Ingredient, Eggs));
        cut.Find(TestId("ingredient-card")).Click();

        DrawerHost.Find($"{TestId("drawer-header-action")} {TestId("drawer-delete-ingredient")}").Click();
        Confirm();

        DrawerHost.WaitForAssertion(() => Assert.NotEmpty(DrawerHost.FindAll(TestId("remove-ingredient-items-question"))));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Empty(Sent<DeleteIngredient.Command>());
    }
}
