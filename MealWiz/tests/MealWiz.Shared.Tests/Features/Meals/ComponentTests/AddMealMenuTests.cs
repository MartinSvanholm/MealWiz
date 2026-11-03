using Bunit;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

public class AddMealMenuTests : ComponentTestBase
{
    [Fact]
    public void Loads_the_latest_import_on_init()
    {
        RenderWithPopovers<AddMealMenu>();

        MealImport.Verify(m => m.LoadLatestImport(), Times.Once);
    }

    [Theory]
    [InlineData(MealImportEntryState.Default, "Import from URL")]
    [InlineData(MealImportEntryState.InProgress, "Import in progress")]
    [InlineData(MealImportEntryState.Failed, "Last import failed: see why")]
    public void Import_item_reflects_the_import_state(MealImportEntryState state, string expectedText)
    {
        MealImport.SetupGet(m => m.EntryState).Returns(state);
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();

        Assert.Contains(expectedText, Popovers.WaitForElement(TestId("add-meal-import")).TextContent);
    }

    [Theory]
    [InlineData(MealImportEntryState.InProgress, "in-progress")]
    [InlineData(MealImportEntryState.Failed, "failed")]
    public void New_button_shows_a_dot_while_an_import_runs_or_has_failed(MealImportEntryState state, string expected)
    {
        MealImport.SetupGet(m => m.EntryState).Returns(state);

        var cut = RenderWithPopovers<AddMealMenu>();

        Assert.Equal(expected, cut.Find(TestId("add-meal-menu-dot")).GetAttribute("data-state"));
    }

    [Fact]
    public void New_button_has_no_dot_without_an_import()
    {
        var cut = RenderWithPopovers<AddMealMenu>();

        Assert.Contains("New", cut.Find(TestId("add-meal-menu")).TextContent);
        Assert.Empty(cut.FindAll(TestId("add-meal-menu-dot")));
    }

    [Fact]
    public void Menu_items_have_hints()
    {
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();

        Assert.Contains("Name, recipe and ingredients", Popovers.WaitForElement(TestId("add-meal-create-manually")).TextContent);
        Assert.Contains("Paste a link to a recipe", Popovers.Find(TestId("add-meal-import")).TextContent);
    }

    [Fact]
    public void Create_manually_starts_a_new_meal_and_opens_the_edit_page()
    {
        var previous = TestData.Meal("Old meal", id: 7);
        Meals.MealToEdit = previous;
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();
        Popovers.WaitForElement(TestId("add-meal-create-manually")).Click();

        cut.WaitForAssertion(() => Assert.Equal("meals/edit", CurrentPath));
        Assert.NotSame(previous, Meals.MealToEdit);
        Assert.Equal(0, Meals.MealToEdit.Id);
    }

    [Fact]
    public void Import_item_opens_the_import_page()
    {
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();
        Popovers.WaitForElement(TestId("add-meal-import")).Click();

        // MudMenuItem closes the menu (async) before it runs OnClick.
        cut.WaitForAssertion(() => Assert.Equal("meals/import", CurrentPath));
    }
}
