using MealWiz.Shared.Features.MealPlans;
using System.Globalization;
using MealWiz.Shared.Features.MealPlans.GetMealPlannedDates;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.SaveGeneratedWeek;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Supabase;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.IntegrationTests;

/// <summary>
/// Runs the Plan week handlers as a freshly signed-in user. The tests share the class's user, so every test plans
/// its own week (<see cref="Week"/>) and never sees another test's plan.
/// </summary>
[Trait("Category", "Integration")]
public class PlanWeekHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    // A Monday far enough ahead that no real plan, and no MealPlanHandlerTests week, is near it.
    private static readonly DateTime FirstMonday = new(2032, 1, 5);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>The Monday of a week only the calling test uses.</summary>
    private static DateTime Week(int number) => FirstMonday.AddDays(7 * number);

    private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task GetMealPlannedDates_ReturnsEachPlannedMealAndDateInTheRange()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var before = await new MealBuilder().InsertAsync(supabase.Client);
        var first = await new MealBuilder().InsertAsync(supabase.Client);
        var last = await new MealBuilder().InsertAsync(supabase.Client);
        var after = await new MealBuilder().InsertAsync(supabase.Client);
        await new MealPlanBuilder().ForWeekOf(Week(0))
            .WithMeal(Week(0).AddDays(2), before).WithMeal(Week(0).AddDays(3), first).WithMeal(Week(0).AddDays(6), last)
            .InsertAsync(supabase.Client);
        await new MealPlanBuilder().ForWeekOf(Week(1)).WithMeal(Week(1), after).InsertAsync(supabase.Client);

        // Both bounds are inclusive.
        var dates = ResultAssert.Ok(await GetAsync(Week(0).AddDays(3), Week(0).AddDays(6)));

        Assert.Equal(
            [new PlannedMealDate(first.Id, Week(0).AddDays(3)), new PlannedMealDate(last.Id, Week(0).AddDays(6))],
            dates);
    }

    [Fact]
    public async Task GetMealPlannedDates_IncludesSystemMeals()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var leftovers = await GetSystemMealAsync(MealTypeExtensions.LeftoverValue);
        await new MealPlanBuilder().ForWeekOf(Week(2)).WithMeal(Week(2).AddDays(1), leftovers).InsertAsync(supabase.Client);

        var dates = ResultAssert.Ok(await GetAsync(Week(2), Week(2).AddDays(6)));

        Assert.Equal([new PlannedMealDate(leftovers.Id, Week(2).AddDays(1))], dates);
    }

    [Fact]
    public async Task GetMealPlannedDates_OtherUsersPlans_AreNotReturned()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        await new MealPlanBuilder().ForWeekOf(Week(3)).WithMeal(Week(3), meal).InsertAsync(supabase.Client);
        var otherUser = await supabase.GetOtherUserAsync();

        var dates = ResultAssert.Ok(await new GetMealPlannedDates.Handler(otherUser.Client)
            .Handle(new GetMealPlannedDates.Query(Week(3), Week(3).AddDays(6)), CancellationToken));

        Assert.Empty(dates);
    }

    [Fact]
    public async Task GetMealPlannedDates_EmptyRange_ReturnsEmpty()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        Assert.Empty(ResultAssert.Ok(await GetAsync(Week(4), Week(4).AddDays(6))));
    }

    [Fact]
    public async Task SaveGeneratedWeek_NoPlanYet_CreatesThePlanAndInsertsTheDays()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var one = await new MealBuilder().InsertAsync(supabase.Client);
        var two = await new MealBuilder().InsertAsync(supabase.Client);

        ResultAssert.Ok(await SaveAsync(Week(5), (Week(5), one.Id), (Week(5).AddDays(6), two.Id)));

        var plan = await GetPlanAsync(Week(5));
        Assert.NotNull(plan);
        Assert.Equal(Iso(Week(5)), plan.StartDate);
        Assert.Equal(Iso(Week(5).AddDays(6)), plan.EndDate);
        Assert.Equal(supabase.UserId, plan.CreatedBy);
        var rows = await GetRowsAsync(plan.Id);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.MealId == one.Id && r.MealDate == Iso(Week(5)));
        Assert.Contains(rows, r => r.MealId == two.Id && r.MealDate == Iso(Week(5).AddDays(6)));
        Assert.All(rows, r => Assert.Equal(supabase.UserId, r.CreatedBy));
    }

    [Fact]
    public async Task SaveGeneratedWeek_ExistingPlan_InsertsOnlyNewDaysAndLeavesOthers()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var wednesday = await new MealBuilder().InsertAsync(supabase.Client);
        var monday = await new MealBuilder().InsertAsync(supabase.Client);
        var plan = await new MealPlanBuilder().ForWeekOf(Week(6)).WithMeal(Week(6).AddDays(2), wednesday).InsertAsync(supabase.Client);

        ResultAssert.Ok(await SaveAsync(Week(6), (Week(6), monday.Id)));

        var rows = await GetRowsAsync(plan.Id);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.MealId == wednesday.Id && r.MealDate == Iso(Week(6).AddDays(2)));
        Assert.Contains(rows, r => r.MealId == monday.Id && r.MealDate == Iso(Week(6)));
        Assert.Single(await GetPlansAsync(Week(6)));
    }

    [Fact]
    public async Task SaveGeneratedWeek_DayFilledMeanwhile_IsUpdatedInPlace()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var old = await new MealBuilder().InsertAsync(supabase.Client);
        var replacement = await new MealBuilder().InsertAsync(supabase.Client);
        var plan = await new MealPlanBuilder().ForWeekOf(Week(7)).WithMeal(Week(7).AddDays(1), old).InsertAsync(supabase.Client);
        var before = Assert.Single(await GetRowsAsync(plan.Id));

        ResultAssert.Ok(await SaveAsync(Week(7), (Week(7).AddDays(1), replacement.Id)));

        var after = Assert.Single(await GetRowsAsync(plan.Id));
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(replacement.Id, after.MealId);
        Assert.NotNull(after.UpdatedAt);
    }

    [Fact]
    public async Task SaveGeneratedWeek_WritesLeftovers()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var leftovers = await GetSystemMealAsync(MealTypeExtensions.LeftoverValue);

        ResultAssert.Ok(await SaveAsync(Week(8), (Week(8).AddDays(1), leftovers.Id)));

        var plan = await GetPlanAsync(Week(8));
        Assert.Equal(leftovers.Id, Assert.Single(await GetRowsAsync(plan!.Id)).MealId);
    }

    [Fact]
    public async Task SaveGeneratedWeek_AnotherUsersPlanForTheWeek_IsUntouched()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var theirMeal = await new MealBuilder().InsertAsync(otherUser.Client);
        var theirPlan = await new MealPlanBuilder().ForWeekOf(Week(9)).WithMeal(Week(9), theirMeal).InsertAsync(otherUser.Client);
        var mine = await new MealBuilder().InsertAsync(supabase.Client);

        ResultAssert.Ok(await SaveAsync(Week(9), (Week(9), mine.Id)));

        var myPlan = await GetPlanAsync(Week(9));
        Assert.NotNull(myPlan);
        Assert.NotEqual(theirPlan.Id, myPlan.Id);
        Assert.Equal(mine.Id, Assert.Single(await GetRowsAsync(myPlan.Id)).MealId);
        var theirRow = Assert.Single((await otherUser.Client.From<MealPlanMealDb>().Where(x => x.MealPlanId == theirPlan.Id).Get(CancellationToken)).Models);
        Assert.Equal(theirMeal.Id, theirRow.MealId);
    }

    [Fact]
    public async Task SaveGeneratedWeek_SignedOut_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new SaveGeneratedWeek.Handler(signedOut, TestLocalizer.For<MealPlansResource>())
            .Handle(new SaveGeneratedWeek.Command(Week(10), [new SaveGeneratedWeek.PlannedDay(Week(10), 1)]), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    [Fact]
    public async Task SaveGeneratedWeek_InvalidDays_FailWithoutWriting()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);

        ResultAssert.Failed(await SaveAsync(Week(11)), "Nothing to save");
        ResultAssert.Failed(await SaveAsync(Week(11), (Week(11).AddDays(7), meal.Id)), "Every day must be in the week");
        ResultAssert.Failed(await SaveAsync(Week(11), (Week(11).AddDays(-1), meal.Id)), "Every day must be in the week");
        ResultAssert.Failed(await SaveAsync(Week(11), (Week(11), meal.Id), (Week(11), meal.Id)), "Each day can only be saved once");

        Assert.Empty(await GetPlansAsync(Week(11)));
    }

    [Fact]
    public async Task SaveGeneratedWeek_AnotherUsersMeal_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var theirMeal = await new MealBuilder().InsertAsync(otherUser.Client);

        var result = await SaveAsync(Week(12), (Week(12), theirMeal.Id));

        Assert.True(result.IsFailed);
        var plan = await GetPlanAsync(Week(12));
        Assert.True(plan is null || (await GetRowsAsync(plan.Id)).Count == 0);
    }

    private Task<FluentResults.Result<List<PlannedMealDate>>> GetAsync(DateTime from, DateTime to) =>
        new GetMealPlannedDates.Handler(supabase.Client).Handle(new GetMealPlannedDates.Query(from, to), CancellationToken);

    private Task<FluentResults.Result> SaveAsync(DateTime weekStart, params (DateTime Date, int MealId)[] days) =>
        new SaveGeneratedWeek.Handler(supabase.Client, TestLocalizer.For<MealPlansResource>()).Handle(
            new SaveGeneratedWeek.Command(weekStart, days.Select(d => new SaveGeneratedWeek.PlannedDay(d.Date, d.MealId)).ToList()),
            CancellationToken);

    private async Task<List<MealPlanDb>> GetPlansAsync(DateTime monday)
    {
        var start = Iso(monday);
        var response = await supabase.Client.From<MealPlanDb>().Where(x => x.StartDate == start).Get(CancellationToken);
        return response.Models;
    }

    private async Task<MealPlanDb?> GetPlanAsync(DateTime monday) => (await GetPlansAsync(monday)).SingleOrDefault();

    private async Task<List<MealPlanMealDb>> GetRowsAsync(int mealPlanId)
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
