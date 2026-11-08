using MealWiz.Shared.Features.Meals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.UnitTests;

/// <summary>The system meals are named by their type in the UI language; a regular meal keeps the name it was given.</summary>
public class MealDisplayNameTests
{
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<MealsResource> Localizer =
        TestLocalizer.For<MealsResource>();

    [Theory]
    [InlineData(MealType.Leftover, "Leftovers")]
    [InlineData(MealType.EatOut, "Eat Out")]
    public void SystemMeal_IsNamedByItsType(MealType type, string expected)
    {
        var meal = new Meal { Name = "whatever is stored", Type = type };

        Assert.Equal(expected, meal.DisplayName(Localizer));
    }

    [Fact]
    public void RegularMeal_KeepsItsName()
    {
        var meal = new Meal { Name = "Lasagne", Type = MealType.Regular };

        Assert.Equal("Lasagne", meal.DisplayName(Localizer));
    }

    [Theory]
    [InlineData(MealType.Leftover, "Rester")]
    [InlineData(MealType.EatOut, "Spis ude")]
    public void SystemMeal_InDanish(MealType type, string expected)
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal(expected, new Meal { Name = "Leftovers", Type = type }.DisplayName(Localizer));
    }
}
