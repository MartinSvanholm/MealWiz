using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.CookMeal;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>The cooking view: a read-only page with the meal's ingredients and recipe, one step per line.</summary>
public class CookMealPageTests : ComponentTestBase
{
    private readonly Meal _chili = TestData.Meal("Chili", id: 5, recipe: "Step one.\n\n  \nStep two.",
        ingredients: [TestData.Ingredient("Kidney beans", "400 g", id: 1), TestData.Ingredient("Salt", id: 2)]);
    private readonly Meal _soup = TestData.Meal("Soup", id: 6);
    private readonly Meal _leftovers = TestData.Meal("Leftovers", id: 2, type: MealType.Leftover);

    public CookMealPageTests()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { _leftovers, _chili, _soup }));
    }

    private IRenderedComponent<CookMealPage> RenderPage(int id, string? from = null)
    {
        Navigation.NavigateTo(from is null ? $"/meals/cook/{id}" : $"/meals/cook/{id}?from={from}");
        return Render<CookMealPage>(p => p.Add(c => c.MealId, id));
    }

    [Fact]
    public void Shows_the_name_the_ingredients_in_order_and_one_step_per_non_blank_line()
    {
        var cut = RenderPage(5);

        Assert.Single(Sent<GetAllMeals.Query>());
        Assert.Equal("Chili", cut.Find(TestId("cook-meal-name")).TextContent.Trim());
        var ingredients = cut.FindAll(TestId("cook-meal-ingredient"));
        Assert.Equal(2, ingredients.Count);
        Assert.Equal("Kidney beans", ingredients[0].QuerySelector(TestId("cook-meal-ingredient-name"))!.TextContent.Trim());
        Assert.Equal("400 g", ingredients[0].QuerySelector(TestId("cook-meal-ingredient-amount"))!.TextContent.Trim());
        Assert.Null(ingredients[1].QuerySelector(TestId("cook-meal-ingredient-amount")));
        Assert.Equal(["Step one.", "Step two."], cut.FindAll(TestId("cook-meal-step")).Select(s => s.TextContent.Trim()));
        Assert.Empty(cut.FindAll(TestId("cook-meal-no-recipe")));
    }

    [Fact]
    public void Is_read_only()
    {
        var cut = RenderPage(5);

        Assert.Empty(cut.FindAll("input, textarea, [contenteditable], select"));
        Assert.Empty(cut.FindAll(TestId("save-meal-button")));
    }

    [Fact]
    public void Meal_without_ingredients_shows_a_note()
    {
        var cut = RenderPage(6);

        Assert.NotNull(cut.Find(TestId("cook-meal-no-ingredients")));
        Assert.Empty(cut.FindAll(TestId("cook-meal-ingredient")));
    }

    [Fact]
    public void Meal_without_a_recipe_shows_the_note_and_add_recipe_opens_edit_meal()
    {
        var cut = RenderPage(6);

        Assert.Equal("No recipe yet", cut.Find(TestId("cook-meal-no-recipe")).QuerySelector(".mw-cook-no-recipe-title")!.TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("cook-meal-step")));

        cut.Find(TestId("cook-meal-add-recipe")).Click();

        Assert.Same(_soup, Meals.MealToEdit);
        Assert.EndsWith("/meals/edit", Navigation.Uri);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(2)]
    public void Unknown_and_system_meals_are_not_found(int id)
    {
        var cut = RenderPage(id);

        Assert.Equal("This meal can't be found", cut.Find(TestId("cook-meal-not-found")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("cook-meal-ingredients")));
        Assert.NotNull(cut.Find(TestId("cook-meal-back")));
    }

    [Theory]
    [InlineData("plan", "/")]
    [InlineData("meals", "/meals")]
    [InlineData("edit", "/meals/edit")]
    [InlineData(null, "/meals")]
    public void Back_goes_to_where_the_page_was_opened_from(string? from, string expected)
    {
        var cut = RenderPage(5, from);

        cut.Find(TestId("cook-meal-back")).Click();

        Assert.Equal(expected, new Uri(Navigation.Uri).AbsolutePath);
    }

    [Fact]
    public void Opened_from_edit_meal_without_it_in_state_back_edits_the_meal_shown()
    {
        // A reload or deep link with ?from=edit starts with an empty form; back must still edit this meal.
        var cut = RenderPage(5, "edit");

        cut.Find(TestId("cook-meal-back")).Click();

        Assert.Equal("/meals/edit", new Uri(Navigation.Uri).AbsolutePath);
        Assert.Same(_chili, Meals.MealToEdit);
    }

    [Fact]
    public async Task Keeps_the_screen_on_while_a_meal_shows_and_lets_it_sleep_when_the_page_goes()
    {
        var cut = RenderPage(5);

        WakeLock.Verify(w => w.RequestAsync(), Times.Once);
        WakeLock.Verify(w => w.ReleaseAsync(), Times.Never);

        await cut.Instance.DisposeAsync();

        WakeLock.Verify(w => w.ReleaseAsync(), Times.Once);
    }

    [Fact]
    public void Does_not_ask_for_the_wake_lock_when_the_meal_is_not_found()
    {
        RenderPage(99);

        WakeLock.Verify(w => w.RequestAsync(), Times.Never);
    }

    [Fact]
    public void Renders_the_same_when_the_wake_lock_is_refused()
    {
        WakeLock.Setup(w => w.RequestAsync()).Returns(new ValueTask<bool>(false));

        var cut = RenderPage(5);

        Assert.Equal(2, cut.FindAll(TestId("cook-meal-step")).Count);
    }
}
