using Bunit;
using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.ComponentTests;

public class MealListTests : ComponentTestBase
{
    private readonly List<Meal> _meals =
    [
        TestData.Meal("Pasta carbonara", id: 1),
        TestData.Meal("Chili con carne", id: 2),
        TestData.Meal("Pad thai", id: 3)
    ];

    [Fact]
    public void Renders_a_row_per_meal()
    {
        var cut = Render<MealList>(p => p.Add(l => l.Meals, _meals));

        var names = cut.FindAll(TestId("meal-list-item")).Select(r => r.TextContent.Trim());
        Assert.Equal(["Pasta carbonara", "Chili con carne", "Pad thai"], names);
    }

    [Fact]
    public void Shows_the_empty_message_when_there_are_no_meals()
    {
        var cut = Render<MealList>(p => p
            .Add(l => l.Meals, [])
            .Add(l => l.EmptyMessage, "Nothing here"));

        Assert.Equal("Nothing here", cut.Find(TestId("meal-list-empty")).TextContent);
        Assert.Empty(cut.FindAll(TestId("meal-list-item")));
    }

    [Fact]
    public void Search_filters_meals_case_insensitively()
    {
        var cut = Render<MealList>(p => p.Add(l => l.Meals, _meals));

        cut.Find(InputOf("meal-list-search")).Input("PA");

        var names = cut.FindAll(TestId("meal-list-item")).Select(r => r.TextContent.Trim());
        Assert.Equal(["Pasta carbonara", "Pad thai"], names);
    }

    [Fact]
    public void Clicking_a_row_invokes_OnItemClick_with_that_meal()
    {
        Meal? clicked = null;
        var cut = Render<MealList>(p => p
            .Add(l => l.Meals, _meals)
            .Add(l => l.OnItemClick, (Meal m) => clicked = m));

        cut.FindAll(TestId("meal-list-item"))[1].Click();

        Assert.Same(_meals[1], clicked);
    }

    [Fact]
    public void Clicking_an_item_action_does_not_click_the_row()
    {
        Meal? rowClicked = null;
        Meal? actionClicked = null;
        RenderFragment<Meal> actions = meal => builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "data-testid", "action");
            builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => actionClicked = meal));
            builder.CloseElement();
        };
        var cut = Render<MealList>(p => p
            .Add(l => l.Meals, _meals)
            .Add(l => l.OnItemClick, (Meal m) => rowClicked = m)
            .Add(l => l.ItemActions, actions));

        cut.FindAll(TestId("action"))[0].Click();

        Assert.Same(_meals[0], actionClicked);
        Assert.Null(rowClicked);
    }
}
