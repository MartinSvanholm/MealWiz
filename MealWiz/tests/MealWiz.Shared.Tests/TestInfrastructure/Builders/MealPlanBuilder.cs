using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using Supabase;

namespace MealWiz.Shared.Tests.TestInfrastructure.Builders;

public sealed class MealPlanBuilder
{
    private DateTime _startDate;
    private DateTime _endDate;
    private readonly Dictionary<DateTime, Meal> _mealOnDate = [];

    public MealPlanBuilder() => ForWeekOf(DateTime.Today);

    /// <summary>The week containing <paramref name="date"/>, the same range the app creates a plan for.</summary>
    public MealPlanBuilder ForWeekOf(DateTime date)
    {
        var week = new MealPlan(date);
        return From(week.StartDate, week.EndDate);
    }

    public MealPlanBuilder From(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate.Date;
        _endDate = endDate.Date;
        return this;
    }

    /// <summary>Plans <paramref name="meal"/> on <paramref name="date"/>. To insert, the meal must already exist (inserted or a system meal).</summary>
    public MealPlanBuilder WithMeal(DateTime date, Meal meal)
    {
        _mealOnDate[date.Date] = meal;
        return this;
    }

    public MealPlan Build() => new(_startDate)
    {
        StartDate = _startDate,
        EndDate = _endDate,
        MealOnDate = new Dictionary<DateTime, Meal>(_mealOnDate)
    };

    /// <summary>Inserts the plan and then one meal_plans-meals row per planned meal, as the client's signed-in user.</summary>
    public async Task<MealPlan> InsertAsync(Client client)
    {
        var userId = client.CurrentUserId();

        var mealPlanDb = Build().MapToMealPlanDb();
        mealPlanDb.CreatedBy = userId;
        mealPlanDb.CreatedAt = DateTime.UtcNow;

        var response = await client.From<MealPlanDb>().Insert(mealPlanDb);
        var mealPlan = new MealPlan(response.Model ?? throw new InvalidOperationException("Inserting a meal plan returned no row."));

        foreach (var (date, meal) in _mealOnDate)
        {
            if (meal.Id == 0) throw new InvalidOperationException("Insert the meal before planning it.");

            await client.From<MealPlanMealDb>().Insert(new MealPlanMealDb
            {
                MealDate = date.ToString("yyyy-MM-dd"),
                MealId = meal.Id,
                MealPlanId = mealPlan.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });

            meal.MealPlan = mealPlan;
            meal.MealDate = date;
            mealPlan.MealOnDate[date] = meal;
        }

        return mealPlan;
    }
}
