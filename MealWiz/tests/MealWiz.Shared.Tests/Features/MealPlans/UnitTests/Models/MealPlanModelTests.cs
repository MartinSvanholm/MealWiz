using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.UnitTests.Models;

public class MealPlanModelTests
{
    [Theory]
    [InlineData("2026-10-21T00:00:00")] // Wednesday
    [InlineData("2026-10-19T00:00:00")] // Monday
    [InlineData("2026-10-25T18:30:00")] // Sunday evening
    public void MealPlan_ForTargetDate_CoversItsMondayToSundayWeekAtMidnight(string targetDate)
    {
        var plan = new MealPlan(DateTime.Parse(targetDate));

        Assert.Equal(new DateTime(2026, 10, 19), plan.StartDate);
        Assert.Equal(new DateTime(2026, 10, 25), plan.EndDate);
        Assert.Equal(0, plan.Id);
        Assert.Empty(plan.MealOnDate);
    }

    [Fact]
    public void MealPlan_FromDbModel_ParsesDatesAndKeysMealsByDate()
    {
        var db = new MealPlanDb
        {
            Id = 5,
            StartDate = "2026-10-19",
            EndDate = "2026-10-25",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
            MealPlanMeals =
            [
                new MealPlanMealDb { MealDate = "2026-10-20", Meal = new MealDb { Id = 1, Name = "Pasta" } },
                new MealPlanMealDb { MealDate = "2026-10-22", Meal = new MealDb { Id = 2, Name = "Leftovers", MealType = MealTypeExtensions.LeftoverValue } }
            ]
        };

        var plan = new MealPlan(db);

        Assert.Equal(5, plan.Id);
        Assert.Equal(new DateTime(2026, 10, 19), plan.StartDate);
        Assert.Equal(new DateTime(2026, 10, 25), plan.EndDate);
        Assert.Equal(db.CreatedBy, plan.CreatedBy);
        Assert.Equal(db.CreatedAt, plan.CreatedAt);
        Assert.Equal(db.UpdatedAt, plan.UpdatedAt);
        Assert.Equal(2, plan.MealOnDate.Count);

        var tuesday = plan.MealOnDate[new DateTime(2026, 10, 20)];
        Assert.Equal("Pasta", tuesday.Name);
        Assert.Same(plan, tuesday.MealPlan);
        Assert.Equal(new DateTime(2026, 10, 20), tuesday.MealDate);

        var thursday = plan.MealOnDate[new DateTime(2026, 10, 22)];
        Assert.Equal(MealType.Leftover, thursday.Type);
        Assert.Same(plan, thursday.MealPlan);
    }

    [Fact]
    public void MealPlan_FromDbModelWithoutMeals_HasAnEmptyDictionary()
    {
        var withNull = new MealPlan(new MealPlanDb { StartDate = "2026-10-19", EndDate = "2026-10-25", MealPlanMeals = null! });
        var withEmpty = new MealPlan(new MealPlanDb { StartDate = "2026-10-19", EndDate = "2026-10-25", MealPlanMeals = [] });

        Assert.Empty(withNull.MealOnDate);
        Assert.Empty(withEmpty.MealOnDate);
    }

    [Fact]
    public void MapToMealPlanDb_WritesDatesAsIsoDays()
    {
        var plan = new MealPlan(new DateTime(2026, 12, 30, 14, 0, 0))
        {
            Id = 5,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc)
        };

        var db = plan.MapToMealPlanDb();

        Assert.Equal(5, db.Id);
        Assert.Equal("2026-12-28", db.StartDate);
        Assert.Equal("2027-01-03", db.EndDate);
        Assert.Equal(plan.CreatedBy, db.CreatedBy);
        Assert.Equal(plan.CreatedAt, db.CreatedAt);
        Assert.Equal(plan.UpdatedAt, db.UpdatedAt);
        Assert.Equal(plan.StartDate, new MealPlan(db).StartDate);
        Assert.Equal(plan.EndDate, new MealPlan(db).EndDate);
    }
}
