using MealWiz.Shared.Features.Meals.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        MealOnDate = [];
    }

    public MealPlan(MealPlanDb mealPlanDb)
    {
        Id = mealPlanDb.Id;
        StartDate = mealPlanDb.StartDate;
        EndDate = mealPlanDb.EndDate;
        CreatedAt = mealPlanDb.CreatedAt;
        CreatedBy = mealPlanDb.CreatedBy;
        UpdatedAt = mealPlanDb.UpdatedAt;
        MealOnDate = mealPlanDb.MealPlanMeals.ToDictionary(x => x.MealDate, x => new Meal(x.Meal));
    }
}