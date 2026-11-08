using System.Globalization;

namespace MealWiz.Shared.Features.MealPlans.Models;

/// <summary>A meal planned on a date, in any of the user's weeks.</summary>
public sealed record PlannedMealDate(int MealId, DateTime Date)
{
    public PlannedMealDate(PlannedMealDateDb db)
        : this(db.MealId, DateTime.Parse(db.MealDate, CultureInfo.InvariantCulture))
    {
    }
}
