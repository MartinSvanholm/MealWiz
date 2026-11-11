using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Features.Meals.Models;

public static class MealDisplayName
{
    /// <summary>
    /// The name to show for <paramref name="meal"/>. The system meals (Leftovers, Eat Out) get their name in the UI
    /// language by their <see cref="MealType"/>, whatever is stored; a regular meal keeps the name the user typed.
    /// </summary>
    public static string DisplayName(this Meal meal, IStringLocalizer<MealsResource> localizer) => meal.Type switch
    {
        MealType.Leftover => localizer["Meal_SystemLeftovers"],
        MealType.EatOut => localizer["Meal_SystemEatOut"],
        _ => meal.Name,
    };
}
