using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.PlanWeek;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.UnitTests.PlanWeek;

public class WeekGeneratorTests
{
    private static readonly DateTime Monday = new(2031, 1, 6);
    private static readonly DateTime Sunday = Monday.AddDays(6);

    private static readonly Meal LeftoversMeal = TestData.Meal("Leftovers", 900, MealType.Leftover);
    private static readonly Meal EatOutMeal = TestData.Meal("Eat Out", 901, MealType.EatOut);

    private static List<Meal> Pool(int count, int firstId = 1) =>
        Enumerable.Range(0, count).Select(i => TestData.Meal($"Meal {firstId + i}", firstId + i)).ToList();

    private static WeekGeneratorInput Input(
        IReadOnlyList<Meal> pool,
        IReadOnlyCollection<PlannedMealDate>? planned = null,
        IReadOnlyDictionary<DateTime, Meal>? locked = null,
        int weeksBack = 3,
        bool leftoversOn = false,
        Meal? leftovers = null) =>
        new(Monday, pool, planned ?? [], locked ?? new Dictionary<DateTime, Meal>(), weeksBack, leftoversOn, leftovers);

    private static GeneratedWeek Run(WeekGeneratorInput input, int seed) => WeekGenerator.Generate(input, new Random(seed));

    private static List<int> Ids(GeneratedWeek week) => week.Days.Select(d => d.Meal!.Id).ToList();

    /// <summary>M = a regular meal, L = Leftovers, EO = Eat Out, - = empty.</summary>
    private static string Shape(GeneratedWeek week) =>
        string.Join(",", week.Days.Select(d =>
            d.Meal == null ? "-"
            : d.Meal.Id == LeftoversMeal.Id ? "L"
            : d.Meal.Id == EatOutMeal.Id ? "EO"
            : "M"));

    public static TheoryData<int> Seeds => [1, 2, 3, 4, 5];

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_EmptyWeek_FillsSevenDaysWithDistinctPoolMeals(int seed)
    {
        var week = Run(Input(Pool(10)), seed);

        Assert.Equal(7, week.Days.Count);
        Assert.Equal(Enumerable.Range(0, 7).Select(i => Monday.AddDays(i)), week.Days.Select(d => d.Date));
        Assert.All(week.Days, d => Assert.NotNull(d.Meal));
        Assert.Equal(7, Ids(week).Distinct().Count());
        Assert.False(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_NeverPicksEatOutOrLeftovers(int seed)
    {
        var pool = Pool(8);
        pool.Add(LeftoversMeal);
        pool.Add(EatOutMeal);

        var week = Run(Input(pool), seed);

        Assert.All(week.Days, d => Assert.True(d.Meal!.IsRegular));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_KeepsLockedDaysAndFillsTheRest(int seed)
    {
        var pool = Pool(10);
        var locked = new Dictionary<DateTime, Meal> { [Monday.AddDays(2)] = pool[3], [Monday.AddDays(5)] = EatOutMeal };

        var week = Run(Input(pool, locked: locked), seed);

        Assert.Equal(pool[3].Id, week.Days[2].Meal!.Id);
        Assert.True(week.Days[2].IsLocked);
        Assert.Equal(EatOutMeal.Id, week.Days[5].Meal!.Id);
        Assert.True(week.Days[5].IsLocked);
        Assert.Equal(5, week.Days.Count(d => !d.IsLocked && d.Meal != null));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_DoesNotRepeatALockedMeal(int seed)
    {
        var pool = Pool(8);
        var locked = new Dictionary<DateTime, Meal> { [Monday.AddDays(2)] = pool[3] };

        var week = Run(Input(pool, locked: locked), seed);

        Assert.Equal(1, Ids(week).Count(id => id == pool[3].Id));
        Assert.Equal(7, Ids(week).Distinct().Count());
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_SkipsMealsPlannedInTheWeeksBefore(int seed)
    {
        var pool = Pool(8);
        var planned = new[] { new PlannedMealDate(pool[0].Id, Monday.AddDays(-14)) };

        var week = Run(Input(pool, planned, weeksBack: 2), seed);

        Assert.DoesNotContain(pool[0].Id, Ids(week));
        Assert.False(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_SkipsMealsPlannedInTheWeeksAfter(int seed)
    {
        var pool = Pool(8);
        var planned = new[] { new PlannedMealDate(pool[0].Id, Sunday.AddDays(14)) };

        var week = Run(Input(pool, planned, weeksBack: 2), seed);

        Assert.DoesNotContain(pool[0].Id, Ids(week));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_SkipsMealsAlreadyPlannedInTheTargetWeek(int seed)
    {
        var pool = Pool(8);
        var planned = new[] { new PlannedMealDate(pool[0].Id, Monday.AddDays(2)) };

        var week = Run(Input(pool, planned, weeksBack: 0), seed);

        Assert.DoesNotContain(pool[0].Id, Ids(week));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_IgnoresPlannedDatesOutsideTheWindow(int seed)
    {
        var pool = Pool(7);
        var planned = new[]
        {
            new PlannedMealDate(pool[0].Id, Monday.AddDays(-8)),
            new PlannedMealDate(pool[1].Id, Sunday.AddDays(8)),
        };

        var week = Run(Input(pool, planned, weeksBack: 1), seed);

        Assert.Equal(pool.Select(m => m.Id).Order(), Ids(week).Order());
        Assert.False(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_WeeksBackZero_OnlyTheTargetWeekCounts(int seed)
    {
        var pool = Pool(7);
        var planned = new[] { new PlannedMealDate(pool[0].Id, Monday.AddDays(-1)) };

        var week = Run(Input(pool, planned, weeksBack: 0), seed);

        Assert.Contains(pool[0].Id, Ids(week));
        Assert.False(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_TooFewFreshMeals_UsesTheLeastRecentlyUsed(int seed)
    {
        var pool = Pool(8); // 5 fresh + E1, E2, E3
        var (e1, e2, e3) = (pool[5], pool[6], pool[7]);
        var planned = new[]
        {
            new PlannedMealDate(e1.Id, Monday.AddDays(-3)),
            new PlannedMealDate(e2.Id, Monday.AddDays(-20)),
            new PlannedMealDate(e3.Id, Sunday.AddDays(10)),
        };

        var week = Run(Input(pool, planned, weeksBack: 3), seed);

        Assert.Equal(7, Ids(week).Distinct().Count());
        Assert.Contains(e2.Id, Ids(week));
        Assert.Contains(e3.Id, Ids(week));
        Assert.DoesNotContain(e1.Id, Ids(week));
        Assert.True(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_Fallback_UsesEachMealsNearestUse(int seed)
    {
        var pool = Pool(8); // 6 fresh + E1, E2
        var (e1, e2) = (pool[6], pool[7]);
        var planned = new[]
        {
            new PlannedMealDate(e1.Id, Monday.AddDays(-20)),
            new PlannedMealDate(e1.Id, Monday.AddDays(-2)),
            new PlannedMealDate(e2.Id, Monday.AddDays(-10)),
        };

        var week = Run(Input(pool, planned, weeksBack: 3), seed);

        Assert.Contains(e2.Id, Ids(week));
        Assert.DoesNotContain(e1.Id, Ids(week));
        Assert.True(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_FewerMealsThanDays_RepeatsOnlyAfterEveryMealIsUsed(int seed)
    {
        var pool = Pool(3);

        var week = Run(Input(pool), seed);

        var ids = Ids(week);
        Assert.Equal(3, ids.Take(3).Distinct().Count());
        Assert.All(pool, m => Assert.InRange(ids.Count(id => id == m.Id), 2, 3));
        Assert.True(week.UsedFallback);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_LeftoversOn_AlternatesMealAndLeftovers(int seed)
    {
        var week = Run(Input(Pool(10), leftoversOn: true, leftovers: LeftoversMeal), seed);

        Assert.Equal("M,L,M,L,M,L,M", Shape(week));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_LeftoversOn_LockedMealCountsAsAMeal(int seed)
    {
        var pool = Pool(10);
        var locked = new Dictionary<DateTime, Meal> { [Monday.AddDays(2)] = pool[0] };

        var week = Run(Input(pool, locked: locked, leftoversOn: true, leftovers: LeftoversMeal), seed);

        Assert.Equal("M,L,M,L,M,L,M", Shape(week));
        Assert.Equal(pool[0].Id, week.Days[2].Meal!.Id);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_LeftoversOn_LockedDayRightAfterAMeal(int seed)
    {
        var pool = Pool(10);
        var locked = new Dictionary<DateTime, Meal> { [Monday.AddDays(1)] = pool[0] };

        var week = Run(Input(pool, locked: locked, leftoversOn: true, leftovers: LeftoversMeal), seed);

        Assert.Equal("M,M,L,M,L,M,L", Shape(week));
        Assert.Equal(pool[0].Id, week.Days[1].Meal!.Id);
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_LeftoversOn_LockedEatOutOrLeftoversIsNotAMeal(int seed)
    {
        var pool = Pool(10);

        var eatOutMonday = Run(Input(pool, locked: new Dictionary<DateTime, Meal> { [Monday] = EatOutMeal },
            leftoversOn: true, leftovers: LeftoversMeal), seed);
        var leftoversTuesday = Run(Input(pool, locked: new Dictionary<DateTime, Meal> { [Monday.AddDays(1)] = LeftoversMeal },
            leftoversOn: true, leftovers: LeftoversMeal), seed);

        Assert.Equal("EO,M,L,M,L,M,L", Shape(eatOutMonday));
        Assert.Equal("M,L,M,L,M,L,M", Shape(leftoversTuesday));
    }

    [Theory, MemberData(nameof(Seeds))]
    public void Generate_LeftoversOnWithoutALeftoversMeal_FillsEveryDay(int seed)
    {
        var week = Run(Input(Pool(10), leftoversOn: true, leftovers: null), seed);

        Assert.Equal("M,M,M,M,M,M,M", Shape(week));
    }

    [Fact]
    public void Generate_EmptyPool_LeavesUnlockedDaysEmpty()
    {
        var week = Run(Input([]), 1);

        Assert.Equal(7, week.Days.Count);
        Assert.All(week.Days, d => Assert.Null(d.Meal));
    }

    [Fact]
    public void Generate_EveryDayLocked_ReturnsThemUnchanged()
    {
        var pool = Pool(7);
        var locked = pool.Select((m, i) => (m, i)).ToDictionary(x => Monday.AddDays(x.i), x => x.m);

        var week = Run(Input(pool, locked: locked), 1);

        Assert.All(week.Days, d => Assert.True(d.IsLocked));
        Assert.Equal(pool.Select(m => m.Id), Ids(week));
        Assert.False(week.UsedFallback);
    }

    [Fact]
    public void Generate_SameSeed_GivesTheSameWeek()
    {
        var input = Input(Pool(12));

        Assert.Equal(Ids(Run(input, 7)), Ids(Run(input, 7)));
    }
}
