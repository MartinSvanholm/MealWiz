using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Meals.EditMeal;
using MealWiz.Shared.Features.MealImport.Models;
using Moq;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>The edit meal form, including adding and removing ingredient rows through the add row.</summary>
public class EditMealPageTests : ComponentTestBase
{
    [Fact]
    public void Renders_the_meal_being_edited_with_its_ingredients()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", ingredients: [TestData.Ingredient("Flour", "200g", id: 1), TestData.Ingredient("Milk", "3 dl", id: 2)]);

        var cut = Render<EditMealPage>();

        Assert.Equal("Pancakes", cut.Find(InputOf("meal-name-input")).GetAttribute("value"));
        var cards = cut.FindAll(TestId("ingredient-card")).Select(c => c.TextContent).ToList();
        Assert.Equal(2, cards.Count);
        Assert.Contains("Flour", cards[0]);
        Assert.Contains("200g", cards[0]);
        Assert.Contains("Milk", cards[1]);
    }

    [Fact]
    public void Typing_a_name_updates_the_meal_being_edited()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<EditMealPage>();

        cut.Find(InputOf("meal-name-input")).Change("Crêpes");

        Assert.Equal("Crêpes", Meals.MealToEdit.Name);
    }

    [Fact]
    public void Enter_in_the_name_moves_to_the_recipe()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        var cut = Render<EditMealPage>();

        PressEnter(cut.Find(InputOf("meal-name-input")));

        AssertFocused(cut.Find(InputOf("meal-recipe-input")));
    }

    [Fact]
    public void Back_returns_to_the_meals_page()
    {
        var cut = Render<EditMealPage>();

        cut.Find(TestId("edit-meal-back")).Click();

        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void Adding_an_ingredient_to_a_new_meal_adds_a_pending_row_without_saving()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);
        RenderDrawers();
        var cut = Render<EditMealPage>();

        cut.Find(InputOf("add-ingredient-name")).Input("Eggs");
        cut.Find(InputOf("add-ingredient-amount")).Input("3");
        cut.Find(TestId("create-ingredient-button")).Click();
        // The add button opens the drawer with what was typed; saving happens there.
        Assert.Empty(cut.FindAll(TestId("ingredient-card")));
        DrawerHost.Find(TestId("save-ingredient-button")).Click();

        var card = cut.WaitForElement(TestId("ingredient-card"));
        Assert.Contains("Eggs", card.TextContent);
        Assert.Contains("3", card.TextContent);
        Assert.Equal("1 item", cut.Find(TestId("edit-meal-ingredient-count")).TextContent.Trim());
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void Deleting_a_pending_ingredient_removes_its_row_without_a_request()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [TestData.Ingredient("Eggs", "3")]);
        var cut = Render<EditMealPage>();

        cut.Find(TestId("delete-ingredient-button")).Click();

        Assert.Empty(cut.FindAll(TestId("ingredient-card")));
        Assert.Empty(Meals.MealToEdit.Ingredients);
        Assert.Empty(SentRequests);
    }

    [Fact]
    public void A_saved_meal_can_be_deleted_from_the_page()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 5);

        var cut = Render<EditMealPage>();

        Assert.NotNull(cut.Find(TestId("delete-meal-button")));
    }

    [Fact]
    public void A_new_meal_has_no_delete_button()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0);

        var cut = Render<EditMealPage>();

        Assert.Empty(cut.FindAll(TestId("delete-meal-button")));
    }

    [Fact]
    public async Task Leaving_the_page_closes_the_drawer()
    {
        Meals.MealToEdit = TestData.Meal("Pancakes", id: 0, ingredients: [TestData.Ingredient("Eggs", "3")]);
        var cut = Render<EditMealPage>();
        cut.Find(TestId("ingredient-card")).Click();
        Assert.True(Drawer.IsDrawerOpen);

        await DisposeComponentsAsync();

        Assert.False(Drawer.IsDrawerOpen);
    }

    // ---- A form filled from a picture job (MEA-143) ----

    private void FormFrom(bool guessed, bool pictureJob = true)
    {
        MealImport.SetupGet(m => m.FormImport).Returns(new MealWiz.Shared.Features.MealImport.Models.MealImport
        {
            PicturePaths = pictureJob ? ["u/j/0.jpg"] : null,
            SourceUrl = pictureJob ? null : "https://example.com/r",
            Status = MealImportStatus.Succeeded,
            ParsedMeal = new ParsedMeal { Name = "Lasagne", IsGuessed = guessed }
        });
    }

    [Fact]
    public void A_guessed_meal_shows_the_note()
    {
        FormFrom(guessed: true);

        var cut = Render<EditMealPage>();

        Assert.Equal("Guessed from your picture, check ingredients and amounts", cut.Find(TestId("edit-meal-guessed-note")).TextContent.Trim());
    }

    [Theory]
    [InlineData(false, true)]  // a written recipe
    [InlineData(false, false)] // a link import
    public void No_note_for_a_written_recipe_or_a_link_import(bool guessed, bool pictureJob)
    {
        FormFrom(guessed, pictureJob);

        var cut = Render<EditMealPage>();

        Assert.Empty(cut.FindAll(TestId("edit-meal-guessed-note")));
    }

    [Fact]
    public void A_hand_made_meal_has_no_note_and_no_retry()
    {
        var cut = Render<EditMealPage>();

        Assert.Empty(cut.FindAll(TestId("edit-meal-guessed-note")));
        Assert.Empty(cut.FindAll(TestId("edit-meal-retry")));
        Assert.Empty(cut.FindAll(TestId("edit-meal-retry-section")));
    }

    [Fact]
    public void The_retry_is_offered_for_a_picture_job_only()
    {
        FormFrom(guessed: false, pictureJob: false);
        Assert.Empty(Render<EditMealPage>().FindAll(TestId("edit-meal-retry")));
    }

    [Fact]
    public void An_empty_hint_cannot_be_sent_and_a_typed_one_is_passed_to_the_retry()
    {
        FormFrom(guessed: true);
        MealImport.Setup(m => m.RetryWithHint(It.IsAny<string>())).ReturnsAsync(Result.Ok());
        var cut = Render<EditMealPage>();

        cut.Find(TestId("edit-meal-retry")).Click();
        Assert.True(cut.Find(TestId("edit-meal-hint-submit")).HasAttribute("disabled"));

        cut.Find(InputOf("edit-meal-hint-input")).Input("   ");
        Assert.True(cut.Find(TestId("edit-meal-hint-submit")).HasAttribute("disabled"));

        cut.Find(InputOf("edit-meal-hint-input")).Input("it's pizza");
        cut.Find(TestId("edit-meal-hint-submit")).Click();

        MealImport.Verify(m => m.RetryWithHint("it's pizza"), Times.Once);
    }

    [Fact]
    public void While_a_retry_runs_the_form_shows_progress_and_no_hint_field()
    {
        FormFrom(guessed: true);
        MealImport.SetupGet(m => m.IsRetrying).Returns(true);

        var cut = Render<EditMealPage>();

        Assert.Contains("Trying again", cut.Find(TestId("edit-meal-retrying")).TextContent);
        Assert.Empty(cut.FindAll(TestId("edit-meal-hint-submit")));
        Assert.Empty(cut.FindAll(TestId("edit-meal-retry")));
    }

    [Fact]
    public void A_failed_retry_shows_why_and_keeps_the_hint_field_open()
    {
        FormFrom(guessed: true);
        MealImport.SetupGet(m => m.RetryErrorMessage).Returns("We couldn't find a meal or a recipe in the picture.");

        var cut = Render<EditMealPage>();

        Assert.Contains("We couldn't find a meal", cut.Find(TestId("edit-meal-retry-error")).TextContent);
        Assert.NotNull(cut.Find(InputOf("edit-meal-hint-input")));
    }

    [Fact]
    public async Task Leaving_during_a_retry_continues_it_in_the_background()
    {
        FormFrom(guessed: true);
        MealImport.SetupGet(m => m.IsRetrying).Returns(true);
        Render<EditMealPage>();

        await DisposeComponentsAsync();

        MealImport.Verify(m => m.ContinueInBackground(), Times.Once);
        MealImport.Verify(m => m.ReleaseFormImport(), Times.Never);
    }

    [Fact]
    public async Task Leaving_the_form_releases_its_pictures()
    {
        FormFrom(guessed: true);
        MealImport.Setup(m => m.ReleaseFormImport()).Returns(Task.CompletedTask);
        Render<EditMealPage>();

        await DisposeComponentsAsync();

        MealImport.Verify(m => m.ReleaseFormImport(), Times.Once);
        MealImport.Verify(m => m.ContinueInBackground(), Times.Never);
    }
}
