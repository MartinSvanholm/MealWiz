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
    [InlineData(MealImportEntryState.Failed, "Last import failed")]
    public void Import_item_reflects_the_import_state(MealImportEntryState state, string expectedText)
    {
        MealImport.SetupGet(m => m.EntryState).Returns(state);
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();

        Assert.Contains(expectedText, Popovers.WaitForElement(TestId("add-meal-import")).TextContent);
    }

    [Fact]
    public void Create_manually_starts_a_new_meal_and_opens_the_edit_page()
    {
        var previous = TestData.Meal("Old meal", id: 7);
        Meals.MealToEdit = previous;
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();
        Popovers.WaitForElement(TestId("add-meal-create-manually")).Click();

        Assert.NotSame(previous, Meals.MealToEdit);
        Assert.Equal(0, Meals.MealToEdit.Id);
        Assert.Equal("meals/edit", CurrentPath);
    }

    [Fact]
    public void Import_item_opens_the_import_page()
    {
        var cut = RenderWithPopovers<AddMealMenu>();

        cut.Find(TestId("add-meal-menu")).Click();
        Popovers.WaitForElement(TestId("add-meal-import")).Click();

        Assert.Equal("meals/import", CurrentPath);
    }
}
