using Bunit;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

public class MealDisplayComponentTests : ComponentTestBase
{
    [Fact]
    public void Shows_the_selected_dates_meal_with_ingredients_and_recipe_steps()
    {
        MealPlan.MealPlan = TestData.MealPlanWith(MealPlan.SelectedDate, TestData.Meal(
            "Pancakes",
            recipe: "Whisk everything\nFry in butter",
            ingredients: [TestData.Ingredient("Flour", "200g"), TestData.Ingredient("Milk", "3 dl")]));

        var cut = Render<MealDisplayComponent>();

        var text = cut.Markup;
        Assert.Contains("Pancakes", text);
        Assert.Contains("200g flour", text);
        Assert.Contains("3 dl milk", text);
        Assert.Contains("Whisk everything", text);
        Assert.Contains("Fry in butter", text);
    }

    [Fact]
    public void Follows_the_meal_plan_when_the_selected_date_changes()
    {
        var today = MealPlan.SelectedDate.Date;
        var plan = TestData.MealPlanWith(today, TestData.Meal("Pancakes", id: 1));
        var tomorrowsMeal = TestData.Meal("Lasagne", id: 2);
        tomorrowsMeal.MealDate = today.AddDays(1);
        plan.MealOnDate[today.AddDays(1)] = tomorrowsMeal;
        MealPlan.MealPlan = plan;
        var cut = Render<MealDisplayComponent>();

        cut.InvokeAsync(() => MealPlan.SelectedDate = today.AddDays(1));

        Assert.Contains("Lasagne", cut.Markup);
        Assert.DoesNotContain("Pancakes", cut.Markup);
    }
}
