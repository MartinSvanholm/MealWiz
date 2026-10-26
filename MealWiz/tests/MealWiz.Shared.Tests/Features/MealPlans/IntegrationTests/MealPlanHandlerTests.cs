using System.Globalization;
using MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;
using MealWiz.Shared.Features.MealPlans.CreateMealPlan;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Supabase;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.IntegrationTests;

/// <summary>
/// Runs the MealPlans handlers as a freshly signed-in user. The tests share the class's user, so every test plans
/// its own week (<see cref="Week"/>) and never sees another test's plan.
/// </summary>
[Trait("Category", "Integration")]
public class MealPlanHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    // A Monday far enough ahead that no real plan is near it.
    private static readonly DateTime FirstMonday = new(2031, 1, 6);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>The Monday of a week only the calling test uses.</summary>
    private static DateTime Week(int number) => FirstMonday.AddDays(7 * number);

    [Fact]
    public async Task CreateMealPlan_InsertsTheWeekOwnedByTheUser()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var wednesday = Week(0).AddDays(2);

        var created = ResultAssert.Ok(await CreateAsync(supabase.Client, new MealPlan(wednesday)));

        Assert.NotEqual(0, created.Id);
        var row = await supabase.Client.From<MealPlanDb>().Where(x => x.Id == created.Id).Single(CancellationToken);
        Assert.NotNull(row);
        Assert.Equal(Week(0).ToString("yyyy-MM-dd"), row.StartDate);
        Assert.Equal(Week(0).AddDays(6).ToString("yyyy-MM-dd"), row.EndDate);
        Assert.Equal(supabase.UserId, row.CreatedBy);
    }

    [Fact]
    public async Task CreateMealPlan_SignedOut_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await CreateAsync(signedOut, new MealPlan(Week(1)));

        ResultAssert.Failed(result, "Not authenticated");
    }

    [Fact]
    public async Task GetMealPlanByDate_FindsThePlanOnEveryDayOfItsWeek()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var plan = await new MealPlanBuilder().ForWeekOf(Week(2)).InsertAsync(supabase.Client);

        for (var day = 0; day < 7; day++)
        {
            var found = ResultAssert.Ok(await GetByDateAsync(Week(2).AddDays(day)));
            Assert.True(found?.Id == plan.Id, $"No plan found for day {day} of the week.");
        }
    }

    [Fact]
    public async Task GetMealPlanByDate_DayBeforeOrAfterTheWeek_ReturnsNull()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        // Weeks 3 and 5 stay empty, so their edges border only week 4's plan.
        await new MealPlanBuilder().ForWeekOf(Week(4)).InsertAsync(supabase.Client);

        Assert.Null(ResultAssert.Ok(await GetByDateAsync(Week(4).AddDays(-1))));
        Assert.Null(ResultAssert.Ok(await GetByDateAsync(Week(4).AddDays(7))));
    }

    [Fact]
    public async Task GetMealPlanByDate_IgnoresTheTimeOfDayAndKind()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var plan = await new MealPlanBuilder().ForWeekOf(Week(6)).InsertAsync(supabase.Client);
        var lateSunday = Week(6).AddDays(7).AddTicks(-1);
        var utcMonday = DateTime.SpecifyKind(Week(6), DateTimeKind.Utc);

        // The handler uses the date part as-is, with no time zone conversion, matching how the app builds the
        // week from local dates (DateTimeExtensions.StartOfWeek / EndOfWeek).
        Assert.Equal(plan.Id, ResultAssert.Ok(await GetByDateAsync(lateSunday))?.Id);
        Assert.Equal(plan.Id, ResultAssert.Ok(await GetByDateAsync(utcMonday))?.Id);
        Assert.Equal(plan.Id, ResultAssert.Ok(await GetByDateAsync(new MealPlan(lateSunday).StartDate))?.Id);
    }

    [Fact]
    public async Task GetMealPlanByDate_IncludesThePlannedMeals()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().WithName("Tacos").InsertAsync(supabase.Client);
        var tuesday = Week(8).AddDays(1);
        await new MealPlanBuilder().ForWeekOf(tuesday).WithMeal(tuesday, meal).InsertAsync(supabase.Client);

        var found = ResultAssert.Ok(await GetByDateAsync(Week(8)));

        Assert.NotNull(found);
        var planned = Assert.Single(found.MealOnDate);
        Assert.Equal(tuesday, planned.Key);
        Assert.Equal(meal.Id, planned.Value.Id);
        Assert.Equal("Tacos", planned.Value.Name);
    }

    [Fact]
    public async Task GetMealPlanByDate_UnderANonGregorianCulture_DoesNotFindThePlan()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var thai = CultureInfo.GetCultureInfo("th-TH");
        Assert.SkipUnless(thai.Calendar is ThaiBuddhistCalendar, "The th-TH culture isn't available (invariant globalization).");
        await new MealPlanBuilder().ForWeekOf(Week(10)).InsertAsync(supabase.Client);

        var original = CultureInfo.CurrentCulture;
        FluentResults.Result<MealPlan?> result;
        try
        {
            CultureInfo.CurrentCulture = thai;
            result = await GetByDateAsync(Week(10));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        // Pins current behaviour, see MEA-37: the date is formatted with the current culture's calendar, so under
        // th-TH the query asks for 2574-01-20 and misses the 2031 plan.
        Assert.Null(ResultAssert.Ok(result));
        Assert.NotNull(ResultAssert.Ok(await GetByDateAsync(Week(10))));
    }

    [Fact]
    public async Task AddMealToMealPlan_PlansOwnAndSystemMealsOnTheirDates()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var plan = await new MealPlanBuilder().ForWeekOf(Week(12)).InsertAsync(supabase.Client);
        var ownMeal = await new MealBuilder().InsertAsync(supabase.Client);
        var leftovers = await GetSystemMealAsync(MealTypeExtensions.LeftoverValue);

        // What MealSelectionList does through the state container: set the plan and date, then add.
        ownMeal.MealPlan = plan;
        ownMeal.MealDate = Week(12);
        leftovers.MealPlan = plan;
        leftovers.MealDate = Week(12).AddDays(1);
        var addedOwn = ResultAssert.Ok(await AddAsync(ownMeal));
        ResultAssert.Ok(await AddAsync(leftovers));

        Assert.Same(ownMeal, addedOwn);
        var rows = await GetPlanRowsAsync(plan.Id);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.MealId == ownMeal.Id && r.MealDate == Week(12).ToString("yyyy-MM-dd"));
        Assert.Contains(rows, r => r.MealId == leftovers.Id && r.MealDate == Week(12).AddDays(1).ToString("yyyy-MM-dd"));
        Assert.All(rows, r => Assert.Equal(supabase.UserId, r.CreatedBy));
    }

    [Fact]
    public async Task AddMealToMealPlan_WithoutAPlanOrDate_FailsWithoutInserting()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var plan = await new MealPlanBuilder().ForWeekOf(Week(14)).InsertAsync(supabase.Client);
        var noPlan = await new MealBuilder().InsertAsync(supabase.Client);
        noPlan.MealDate = Week(14);
        var noDate = await new MealBuilder().InsertAsync(supabase.Client);
        noDate.MealPlan = plan;

        ResultAssert.Failed(await AddAsync(noPlan), "Meal plan or meal date cannot be empty");
        ResultAssert.Failed(await AddAsync(noDate), "Meal plan or meal date cannot be empty");

        Assert.Empty(await GetPlanRowsAsync(plan.Id));
    }

    [Fact]
    public async Task RemoveMealFromMealPlan_RemovesThePlannedMeal()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        var keep = await new MealBuilder().InsertAsync(supabase.Client);
        var plan = await new MealPlanBuilder()
            .ForWeekOf(Week(16))
            .WithMeal(Week(16), meal)
            .WithMeal(Week(16).AddDays(1), keep)
            .InsertAsync(supabase.Client);

        var result = await RemoveAsync(meal.Id);

        ResultAssert.Ok(result);
        var remaining = Assert.Single(await GetPlanRowsAsync(plan.Id));
        Assert.Equal(keep.Id, remaining.MealId);
    }

    [Fact]
    public async Task RemoveMealFromMealPlan_MealPlannedOnSeveralDates_RemovesEveryOne()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        var thisWeek = await new MealPlanBuilder()
            .ForWeekOf(Week(18))
            .WithMeal(Week(18), meal)
            .WithMeal(Week(18).AddDays(3), meal)
            .InsertAsync(supabase.Client);
        var nextWeek = await new MealPlanBuilder().ForWeekOf(Week(19)).WithMeal(Week(19), meal).InsertAsync(supabase.Client);

        // The Remove button means "this date", but the command only carries the meal id.
        var result = await RemoveAsync(meal.Id);

        // Pins current behaviour, see MEA-35: the delete filters on fk_meal alone, so the meal goes from every date
        // in every plan. With a system meal like Leftovers that empties all of the user's Leftovers days.
        ResultAssert.Ok(result);
        Assert.Empty(await GetPlanRowsAsync(thisWeek.Id));
        Assert.Empty(await GetPlanRowsAsync(nextWeek.Id));
    }

    private Task<FluentResults.Result<MealPlan>> CreateAsync(Client client, MealPlan mealPlan) =>
        new CreateMealPlan.Handler(client).Handle(new CreateMealPlan.Command(mealPlan), CancellationToken);

    private Task<FluentResults.Result<MealPlan?>> GetByDateAsync(DateTime date) =>
        new GetMealPlanByDate.Handler(supabase.Client).Handle(new GetMealPlanByDate.Query(date), CancellationToken);

    private Task<FluentResults.Result<Meal>> AddAsync(Meal meal) =>
        new AddMealToMealPlan.Handler(supabase.Client).Handle(new AddMealToMealPlan.Command(meal), CancellationToken);

    private Task<FluentResults.Result> RemoveAsync(int mealId) =>
        new RemoveMealFromMealPlan.Handler(supabase.Client).Handle(new RemoveMealFromMealPlan.Command(mealId), CancellationToken);

    private async Task<List<MealPlanMealDb>> GetPlanRowsAsync(int mealPlanId)
    {
        var response = await supabase.Client.From<MealPlanMealDb>().Where(x => x.MealPlanId == mealPlanId).Get(CancellationToken);
        return response.Models;
    }

    private async Task<Meal> GetSystemMealAsync(string mealType)
    {
        var response = await supabase.Client.From<MealDb>().Where(x => x.MealType == mealType).Get(CancellationToken);
        return new Meal(Assert.Single(response.Models, m => m.CreatedBy is null));
    }
}
