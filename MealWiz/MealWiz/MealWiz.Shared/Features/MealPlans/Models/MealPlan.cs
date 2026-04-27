using System.Globalization;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;

namespace MealWiz.Shared.Features.MealPlans.Models;

public class MealPlan
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<DateTime, Meal> MealOnDate { get; set; }

    public MealPlan(DateTime targetDate)
    {
        StartDate = targetDate.StartOfWeek().Date;
        EndDate = targetDate.EndOfWeek().Date;
        MealOnDate = [];
    }

    public MealPlan(MealPlanDb mealPlanDb)
    {
        Id = mealPlanDb.Id;
        StartDate = DateTime.Parse(mealPlanDb.StartDate, CultureInfo.InvariantCulture);
        EndDate = DateTime.Parse(mealPlanDb.EndDate, CultureInfo.InvariantCulture);
        CreatedAt = mealPlanDb.CreatedAt;
        CreatedBy = mealPlanDb.CreatedBy;
        UpdatedAt = mealPlanDb.UpdatedAt;
        MealOnDate = mealPlanDb.MealPlanMeals != null && mealPlanDb.MealPlanMeals.Count > 0 ?
            mealPlanDb.MealPlanMeals.ToDictionary(x => DateTime.Parse(x.MealDate, CultureInfo.InvariantCulture), x => new Meal(x.Meal, this, x.MealDate))
            : [];
    }

    public MealPlanDb MapToMealPlanDb()
    {
        return new MealPlanDb()
        {
            Id = Id,
            StartDate = StartDate.ToString("yyyy-MM-dd"),
            EndDate = EndDate.ToString("yyyy-MM-dd"),
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            UpdatedAt = UpdatedAt
        };
    }
}