using MealWiz.Shared.Features.Meals.Models;

namespace MealWiz.Shared.Features.MealPlans.Models;

/// <summary>One day of the Plan week preview. <paramref name="SavedMealId"/> is the meal already saved on the day, if any.</summary>
public sealed record PlanWeekDay(DateTime Date, Meal? Meal, int? SavedMealId, bool IsLocked)
{
    public bool IsChanged => Meal?.Id != SavedMealId;
}
