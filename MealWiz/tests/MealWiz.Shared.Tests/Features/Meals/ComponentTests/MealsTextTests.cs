using Bunit;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.EditMeal;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

/// <summary>
/// The Meals screens' texts come from resources (UI-03): every localizer here echoes its key as <c>[Key]</c>, so a
/// hard-coded text would fail these tests. The English texts themselves are asserted by the other Meals tests.
/// </summary>
public class MealsTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void MealsPage_header_filters_grid_and_import_entry()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal> { TestData.Meal("Pasta", id: 1) }));
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Fail("offline"));

        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal("[MealsPage_Title]", cut.Find(TestId("screen-header-title")).TextContent.Trim());
        Assert.Equal("[MealsPage_Count_One]", cut.Find(TestId("screen-header-label")).TextContent.Trim());
        Assert.Equal("[MealsPage_FilterAll]", cut.Find(TestId("meal-filter-all")).TextContent.Trim());
        Assert.Equal("[MealGrid_Ingredients_Other]", cut.Find(TestId("meal-card-meta")).TextContent.Trim());
        Assert.Contains("[MealsPage_ImportTitle]", cut.Find(TestId("meals-import-link")).TextContent);
        Assert.Contains("[AddMealMenu_New]", cut.Find(TestId("add-meal-menu")).TextContent);
    }

    [Fact]
    public void MealsPage_empty_text()
    {
        SetupSend<GetAllMeals.Query, Result<List<Meal>>>(Result.Ok(new List<Meal>()));
        SetupSend<GetMealPlanByDate.Query, Result<MealPlan?>>(Result.Fail("offline"));

        var cut = RenderWithPopovers<MealsPage>();

        Assert.Equal("[MealsPage_Empty]", cut.Find(TestId("meal-list-empty")).TextContent.Trim());
    }

    [Fact]
    public void EditMealPage_labels()
    {
        Meals.MealToEdit = TestData.Meal("Pasta", id: 0);

        var cut = RenderWithPopovers<EditMealPage>();

        Assert.Equal("[EditMeal_Back]", cut.Find(TestId("edit-meal-back")).GetAttribute("aria-label"));
        Assert.Equal("[EditMeal_NamePlaceholder]", cut.Find(TestId("meal-name-input")).GetAttribute("placeholder"));
        Assert.Equal("[EditMeal_Items_Other]", cut.Find(TestId("edit-meal-ingredient-count")).TextContent.Trim());
        Assert.Equal("[Common_Save]", cut.Find(TestId("save-meal-button")).TextContent.Trim());
    }
}
