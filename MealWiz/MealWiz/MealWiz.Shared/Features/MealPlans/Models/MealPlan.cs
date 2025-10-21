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

    public MealPlan()
    {
        StartDate = DateTime.Now.StartOfWeek().Date;
        EndDate = DateTime.Now.EndOfWeek().Date;
        MealOnDate = [];
    }

    public MealPlan(MealPlanDb mealPlanDb)
    {
        Id = mealPlanDb.Id;
        StartDate = DateTime.Parse(mealPlanDb.StartDate);
        EndDate = DateTime.Parse(mealPlanDb.EndDate);
        CreatedAt = mealPlanDb.CreatedAt;
        CreatedBy = mealPlanDb.CreatedBy;
        UpdatedAt = mealPlanDb.UpdatedAt;
        MealOnDate = mealPlanDb.MealPlanMeals != null && mealPlanDb.MealPlanMeals.Any() ? 
            mealPlanDb.MealPlanMeals.ToDictionary(x => x.MealDate, x => new Meal(x.Meal))
            : [];
    }

    public MealPlanDb MapToMealPlanDb()
    {
        return new MealPlanDb()
        {
            Id = Id,
            StartDate = StartDate.ToString("MM/dd/yyyy"),
            EndDate = EndDate.ToString("MM/dd/yyyy"),
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            UpdatedAt = UpdatedAt
        };
    }
}