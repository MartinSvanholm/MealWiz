using System.Text.Json;
using Blazored.LocalStorage;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.GetMealPlannedDates;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.SaveGeneratedWeek;
using MealWiz.Shared.Features.MealPlans.State;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MudBlazor;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.UnitTests.State;

/// <summary>The clock is faked at Wednesday 2026-11-04, so this week starts Monday 2026-11-02.</summary>
public class PlanWeekStateContainerTests
{
    private static readonly DateTime ThisMonday = new(2026, 11, 2);
    private static readonly DateTime NextMonday = ThisMonday.AddDays(7);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILocalStorageService> _localStorage = new();
    private readonly Mock<IMealPlanStateContainer> _mealPlanState = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly FakeNavigationManager _navigation = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly List<string> _calls = [];
    private readonly PlanWeekStateContainer _container;
    private int _notifications;

    private readonly List<Meal> _meals = [.. Enumerable.Range(1, 10).Select(i => TestData.Meal($"Meal {i}", i))];
    private readonly Meal _leftovers = TestData.Meal("Leftovers", 900, MealType.Leftover);
    private readonly Meal _eatOut = TestData.Meal("Eat Out", 901, MealType.EatOut);

    public PlanWeekStateContainerTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _container = new PlanWeekStateContainer(_mediator.Object, _localStorage.Object, _time, _mealPlanState.Object, _navigation)
        {
            CurrentSnackbar = _snackbar.Object,
            Random = new Random(1)
        };
        _container.OnStateChanged += () => _notifications++;

        SetupMeals([.. _meals, _leftovers, _eatOut]);
        SetupWeek(null);
        SetupPlannedDates([]);
        _mealPlanState.Setup(m => m.NavigateToWeek(It.IsAny<DateTime>())).Returns(Task.CompletedTask).Callback<DateTime>(d => _calls.Add($"meal-plan:{d:yyyy-MM-dd}"));
        _mealPlanState.Setup(m => m.OfferWeekIngredients(It.IsAny<DateTime>())).Callback<DateTime>(d => _calls.Add($"offer:{d:yyyy-MM-dd}"));
        _navigation.OnNavigate = uri => _calls.Add($"navigate:{uri}");
    }

    [Fact]
    public async Task Open_PastWeek_ClampsToThisWeek()
    {
        await _container.Open(new DateTime(2026, 10, 14));

        Assert.Equal(ThisMonday, _container.WeekStart);
        Assert.True(_container.IsThisWeek);
        _mediator.Verify(m => m.Send(new GetMealPlanByDate.Query(ThisMonday), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Open_FutureWeek_LoadsItsPlanWithPlannedDaysLocked()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));

        await _container.Open(NextMonday.AddDays(4));

        Assert.Equal(NextMonday, _container.WeekStart);
        Assert.False(_container.IsThisWeek);
        Assert.False(_container.IsLoading);
        Assert.Equal(7, _container.Days.Count);
        Assert.Equal(Enumerable.Range(0, 7).Select(i => NextMonday.AddDays(i)), _container.Days.Select(d => d.Date));
        var wednesday = _container.Days[2];
        Assert.Equal(_meals[3].Id, wednesday.Meal!.Id);
        Assert.Equal(_meals[3].Id, wednesday.SavedMealId);
        Assert.True(wednesday.IsLocked);
        Assert.All(_container.Days.Where(d => d.Meal is null), d => Assert.False(d.IsLocked));
        Assert.False(_container.IsDirty);
    }

    [Fact]
    public async Task Open_NoSettingsStored_UsesDefaults()
    {
        await _container.Open(ThisMonday);

        Assert.Equal(3, _container.Settings.WeeksBack);
        Assert.False(_container.Settings.LeftoversAfterEveryMeal);
    }

    [Fact]
    public async Task Open_StoredSettings_AreUsed()
    {
        SetupSettings(new PlanWeekSettings(5, true));

        await _container.Open(ThisMonday);

        Assert.Equal(new PlanWeekSettings(5, true), _container.Settings);
    }

    [Fact]
    public async Task Open_CorruptSettings_UsesDefaults()
    {
        _localStorage
            .Setup(s => s.GetItemAsync<PlanWeekSettings>(PlanWeekStateContainer.SettingsStorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException());

        await _container.Open(ThisMonday);

        Assert.Equal(new PlanWeekSettings(), _container.Settings);
        Assert.Equal(7, _container.Days.Count);
    }

    [Theory]
    [InlineData(12, 8)]
    [InlineData(-2, 0)]
    public async Task Open_WeeksBackOutOfRange_IsClamped(int stored, int expected)
    {
        SetupSettings(new PlanWeekSettings(stored));

        await _container.Open(ThisMonday);

        Assert.Equal(expected, _container.Settings.WeeksBack);
    }

    [Fact]
    public async Task Open_NoRegularMeals_HasNoRegularMeals()
    {
        SetupMeals([_leftovers, _eatOut]);

        await _container.Open(ThisMonday);

        Assert.True(_container.HasNoRegularMeals);
        Assert.False(_container.CanGenerate);
    }

    [Fact]
    public async Task Open_WithMeals_HasRegularMealsOnly()
    {
        await _container.Open(ThisMonday);

        Assert.False(_container.HasNoRegularMeals);
        Assert.Equal(_meals.Count, _container.RegularMeals.Count);
        Assert.True(_container.CanGenerate);
    }

    [Fact]
    public async Task Open_MealsLoadFails_ShowsTheErrorAndCannotGenerate()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetAllMeals.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<Meal>>("Meals broke"));

        await _container.Open(ThisMonday);

        VerifyToast("Meals broke", Severity.Error);
        Assert.Empty(_container.Days);
        Assert.False(_container.IsLoading);
        Assert.False(_container.CanGenerate);
        Assert.False(_container.CanSave);
    }

    [Fact]
    public async Task Open_WeekLoadFails_ShowsTheErrorAndCannotGenerate()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMealPlanByDate.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealPlan?>("Week broke"));

        await _container.Open(ThisMonday);

        VerifyToast("Week broke", Severity.Error);
        Assert.Empty(_container.Days);
        Assert.False(_container.IsLoading);
        Assert.False(_container.CanGenerate);
        Assert.False(_container.CanSave);
    }

    [Fact]
    public async Task NavigateToWeek_BeforeThisWeek_StaysOnThisWeek()
    {
        await _container.Open(NextMonday);

        await _container.NavigateToWeek(ThisMonday.AddDays(-7));

        Assert.Equal(ThisMonday, _container.WeekStart);
    }

    [Fact]
    public async Task NavigateToWeek_DiscardsThePreview()
    {
        await _container.Open(ThisMonday);
        await _container.Generate();
        Assert.True(_container.IsDirty);

        await _container.NavigateToWeek(NextMonday);

        Assert.Equal(NextMonday, _container.WeekStart);
        Assert.False(_container.IsDirty);
        Assert.False(_container.UsedFallback);
        Assert.All(_container.Days, d => Assert.Null(d.Meal));
    }

    [Fact]
    public async Task NavigateToWeek_IgnoresAStaleLoad()
    {
        var first = new TaskCompletionSource<Result<MealPlan?>>();
        _mediator.SetupSequence(m => m.Send(It.IsAny<GetMealPlanByDate.Query>(), It.IsAny<CancellationToken>()))
            .Returns(first.Task)
            .ReturnsAsync(Result.Ok<MealPlan?>(PlanWith(NextMonday.AddDays(7), _meals[1])));
        var slow = _container.Open(NextMonday);

        await _container.NavigateToWeek(NextMonday.AddDays(7));
        first.SetResult(Result.Ok<MealPlan?>(PlanWith(NextMonday, _meals[0])));
        await slow;

        Assert.Equal(NextMonday.AddDays(7), _container.WeekStart);
        Assert.Equal(_meals[1].Id, _container.Days[0].Meal!.Id);
    }

    [Fact]
    public async Task SetWeeksBack_ClampsAndStoresTheSettings()
    {
        await _container.Open(ThisMonday);

        await _container.SetWeeksBack(20);

        Assert.Equal(8, _container.Settings.WeeksBack);
        _localStorage.Verify(s => s.SetItemAsync(PlanWeekStateContainer.SettingsStorageKey, new PlanWeekSettings(8), It.IsAny<CancellationToken>()), Times.Once);

        await _container.SetWeeksBack(-4);

        Assert.Equal(0, _container.Settings.WeeksBack);
        _localStorage.Verify(s => s.SetItemAsync(PlanWeekStateContainer.SettingsStorageKey, new PlanWeekSettings(0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetWeeksBack_Unchanged_StoresNothing()
    {
        await _container.Open(ThisMonday);

        await _container.SetWeeksBack(3);

        _localStorage.Verify(s => s.SetItemAsync(It.IsAny<string>(), It.IsAny<PlanWeekSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetLeftoversAfterEveryMeal_StoresTheSettings()
    {
        await _container.Open(ThisMonday);

        await _container.SetLeftoversAfterEveryMeal(true);

        Assert.True(_container.Settings.LeftoversAfterEveryMeal);
        _localStorage.Verify(s => s.SetItemAsync(PlanWeekStateContainer.SettingsStorageKey, new PlanWeekSettings(3, true), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleLock_DayWithAMeal_FlipsIt_EmptyDay_DoesNothing()
    {
        SetupWeek(PlanWith(ThisMonday.AddDays(1), _meals[0]));
        await _container.Open(ThisMonday);
        _notifications = 0;

        _container.ToggleLock(ThisMonday.AddDays(1));
        Assert.False(_container.Days[1].IsLocked);
        _container.ToggleLock(ThisMonday.AddDays(1));
        Assert.True(_container.Days[1].IsLocked);
        Assert.Equal(2, _notifications);

        _container.ToggleLock(ThisMonday.AddDays(3));
        Assert.False(_container.Days[3].IsLocked);
    }

    [Theory]
    [InlineData(3, -21, 27)]
    [InlineData(0, 0, 6)]
    public async Task Generate_SendsTheWindowForWeeksBack(int weeksBack, int fromOffset, int toOffset)
    {
        SetupSettings(new PlanWeekSettings(weeksBack));
        await _container.Open(NextMonday);

        await _container.Generate();

        _mediator.Verify(m => m.Send(
            new GetMealPlannedDates.Query(NextMonday.AddDays(fromOffset), NextMonday.AddDays(toOffset)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Generate_KeepsLockedDaysAndFillsTheOthers()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        await _container.Open(NextMonday);

        await _container.Generate();

        Assert.Equal(_meals[3].Id, _container.Days[2].Meal!.Id);
        Assert.True(_container.Days[2].IsLocked);
        Assert.All(_container.Days, d => Assert.NotNull(d.Meal));
        Assert.Equal(7, _container.Days.Select(d => d.Meal!.Id).Distinct().Count());
        Assert.All(_container.Days.Where(d => d.Date != NextMonday.AddDays(2)), d => Assert.Null(d.SavedMealId));
        Assert.True(_container.IsDirty);
        Assert.False(_container.IsGenerating);
    }

    [Fact]
    public async Task Generate_UnlockedPlannedDay_IsRegenerated()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        await _container.Open(NextMonday);
        _container.ToggleLock(NextMonday.AddDays(2));
        // The planned meal counts as used in the target week, so it can't come back.
        SetupPlannedDates([new PlannedMealDate(_meals[3].Id, NextMonday.AddDays(2))]);

        await _container.Generate();

        var wednesday = _container.Days[2];
        Assert.NotEqual(_meals[3].Id, wednesday.Meal!.Id);
        Assert.Equal(_meals[3].Id, wednesday.SavedMealId);
        Assert.True(wednesday.IsChanged);
    }

    [Fact]
    public async Task Generate_WithLeftoversOn_AlternatesWithTheLeftoversMeal()
    {
        SetupSettings(new PlanWeekSettings(3, true));
        await _container.Open(NextMonday);

        await _container.Generate();

        Assert.Equal([_leftovers.Id], _container.Days.Where(d => d.Meal!.Type == MealType.Leftover).Select(d => d.Meal!.Id).Distinct());
        Assert.Equal(3, _container.Days.Count(d => d.Meal!.Type == MealType.Leftover));
    }

    [Fact]
    public async Task Generate_PlannedDatesFail_LeavesDaysUnchanged()
    {
        await _container.Open(NextMonday);
        _mediator.Setup(m => m.Send(It.IsAny<GetMealPlannedDates.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<PlannedMealDate>>("Dates broke"));

        await _container.Generate();

        VerifyToast("Dates broke", Severity.Error);
        Assert.All(_container.Days, d => Assert.Null(d.Meal));
        Assert.False(_container.IsGenerating);
        Assert.True(_container.CanGenerate);
    }

    [Fact]
    public async Task Generate_SetsUsedFallback()
    {
        SetupMeals([.. _meals.Take(3)]);
        await _container.Open(NextMonday);

        await _container.Generate();

        Assert.True(_container.UsedFallback);
        Assert.All(_container.Days, d => Assert.NotNull(d.Meal));
    }

    [Fact]
    public async Task Generate_EnoughMeals_DoesNotSetUsedFallback()
    {
        await _container.Open(NextMonday);

        await _container.Generate();

        Assert.False(_container.UsedFallback);
    }

    [Fact]
    public async Task Save_SendsOnlyChangedDays()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        await _container.Open(NextMonday);
        await _container.Generate();
        SetupSave(Result.Ok().WithSuccess("Week saved"));

        await _container.Save();

        var sent = Assert.Single(_mediator.Invocations, i => i.Arguments[0] is SaveGeneratedWeek.Command).Arguments[0] as SaveGeneratedWeek.Command;
        Assert.NotNull(sent);
        Assert.Equal(NextMonday, sent.WeekStart);
        Assert.Equal(6, sent.Days.Count);
        Assert.DoesNotContain(sent.Days, d => d.Date == NextMonday.AddDays(2));
        Assert.All(sent.Days, d => Assert.Equal(_container.Days.Single(x => x.Date == d.Date).Meal!.Id, d.MealId));
    }

    [Fact]
    public async Task Save_Success_NavigatesTheMealPlanToTheWeekThenHome()
    {
        await _container.Open(NextMonday);
        await _container.Generate();
        SetupSave(Result.Ok().WithSuccess("Week saved"));

        var saved = await _container.Save();

        Assert.True(saved);
        // The offer comes after the week is loaded and before the Meal plan page is shown (MEA-83).
        Assert.Equal([$"meal-plan:{NextMonday:yyyy-MM-dd}", $"offer:{NextMonday:yyyy-MM-dd}", "navigate:"], _calls);
        Assert.False(_container.IsDirty);
        Assert.False(_container.IsSaving);
        VerifyToast("Week saved", Severity.Success);
    }

    [Fact]
    public async Task Save_Failure_StaysAndKeepsThePreview()
    {
        await _container.Open(NextMonday);
        await _container.Generate();
        SetupSave(Result.Fail("Save broke"));

        var saved = await _container.Save();

        Assert.False(saved);
        Assert.Empty(_calls); // no navigation and no offer
        Assert.True(_container.IsDirty);
        Assert.True(_container.CanSave);
        Assert.False(_container.IsSaving);
        VerifyToast("Save broke", Severity.Error);
    }

    [Fact]
    public async Task Save_NothingChanged_SendsNothing()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        await _container.Open(NextMonday);

        var saved = await _container.Save();

        Assert.False(saved);
        Assert.False(_container.CanSave);
        _mediator.Verify(m => m.Send(It.IsAny<SaveGeneratedWeek.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Adjusting the preview (MEA-80) ----

    private async Task<IPlanWeekStateContainer> OpenAndGenerate(DateTime? week = null)
    {
        await _container.Open(week ?? NextMonday);
        await _container.Generate();
        return _container;
    }

    private int PlannedDatesQueries() =>
        _mediator.Invocations.Count(i => i.Method.Name == nameof(IMediator.Send) && i.Arguments[0] is GetMealPlannedDates.Query);

    [Fact]
    public async Task Generate_SetsHasPreview()
    {
        await _container.Open(NextMonday);
        Assert.False(_container.HasPreview);

        await _container.Generate();

        Assert.True(_container.HasPreview);
    }

    [Fact]
    public async Task Open_And_NavigateToWeek_ClearHasPreview()
    {
        await OpenAndGenerate();

        await _container.NavigateToWeek(NextMonday.AddDays(7));
        Assert.False(_container.HasPreview);

        await _container.Generate();
        await _container.Open(NextMonday);
        Assert.False(_container.HasPreview);
    }

    [Fact]
    public async Task ShuffleDay_UnlockedDay_PutsAnotherRegularMealNotOnOtherDays()
    {
        await OpenAndGenerate();
        var date = NextMonday.AddDays(1);
        var before = _container.Days[1].Meal!;
        var others = _container.Days.Where(d => d.Date != date).Select(d => d.Meal!.Id).ToHashSet();

        await _container.ShuffleDay(date);

        var after = _container.Days[1].Meal!;
        Assert.NotEqual(before.Id, after.Id);
        Assert.True(after.IsRegular);
        Assert.DoesNotContain(after.Id, others);
        Assert.False(_container.Days[1].IsLocked);
    }

    [Fact]
    public async Task ShuffleDay_LockedDay_ChangesNothing()
    {
        await OpenAndGenerate();
        var date = NextMonday.AddDays(1);
        _container.ToggleLock(date);
        var before = _container.Days.ToList();

        await _container.ShuffleDay(date);

        Assert.Equal(before, _container.Days);
    }

    [Fact]
    public async Task ShuffleDay_EmptyUnlockedDay_FillsIt()
    {
        await OpenAndGenerate();
        var date = NextMonday.AddDays(3);
        _container.ClearDay(date);
        Assert.Null(_container.Days[3].Meal);

        await _container.ShuffleDay(date);

        Assert.NotNull(_container.Days[3].Meal);
    }

    [Fact]
    public async Task ShuffleDay_NoOtherMeal_LeavesTheDay()
    {
        SetupMeals([_meals[0], _leftovers, _eatOut]);
        await OpenAndGenerate();
        var before = _container.Days.ToList();

        await _container.ShuffleDay(NextMonday);

        Assert.Equal(before, _container.Days);
    }

    [Fact]
    public async Task ShuffleDay_Notifies()
    {
        await OpenAndGenerate();
        _notifications = 0;

        await _container.ShuffleDay(NextMonday.AddDays(1));

        Assert.True(_notifications > 0);
    }

    [Fact]
    public async Task ShuffleDay_WeeksBackChangedSinceGenerate_ReloadsTheInputs()
    {
        await OpenAndGenerate();
        var queries = PlannedDatesQueries();

        await _container.ShuffleDay(NextMonday.AddDays(1));
        Assert.Equal(queries, PlannedDatesQueries());

        await _container.SetWeeksBack(5);
        await _container.ShuffleDay(NextMonday.AddDays(1));

        Assert.Equal(queries + 1, PlannedDatesQueries());
    }

    [Fact]
    public async Task ShuffleAll_RerollsUnlockedDaysAndKeepsLockedOnes()
    {
        await OpenAndGenerate();
        var lockedDate = NextMonday.AddDays(2);
        _container.ToggleLock(lockedDate);
        var lockedMeal = _container.Days[2].Meal!;
        var before = _container.Days.Select(d => d.Meal!.Id).ToList();

        await _container.ShuffleAll();

        Assert.Same(lockedMeal, _container.Days[2].Meal);
        Assert.NotEqual(before, _container.Days.Select(d => d.Meal!.Id).ToList());
        Assert.True(_container.HasPreview);
    }

    [Fact]
    public async Task ShuffleAll_FillsClearedDays()
    {
        await OpenAndGenerate();
        _container.ClearDay(NextMonday.AddDays(4));

        await _container.ShuffleAll();

        Assert.All(_container.Days, d => Assert.NotNull(d.Meal));
    }

    [Fact]
    public async Task PickMeal_SetsTheMealLocksTheDayAndMarksDirty()
    {
        await _container.Open(NextMonday);
        Assert.False(_container.IsDirty);

        _container.PickMeal(NextMonday.AddDays(1), _meals[4]);

        var day = _container.Days[1];
        Assert.Same(_meals[4], day.Meal);
        Assert.True(day.IsLocked);
        Assert.True(_container.IsDirty);
    }

    [Fact]
    public async Task PickMeal_AcceptsEatOutAndLeftovers()
    {
        await _container.Open(NextMonday);

        _container.PickMeal(NextMonday, _eatOut);
        _container.PickMeal(NextMonday.AddDays(1), _leftovers);

        Assert.Same(_eatOut, _container.Days[0].Meal);
        Assert.Same(_leftovers, _container.Days[1].Meal);
    }

    [Fact]
    public async Task PickMeal_OnALockedDay_ReplacesTheMeal()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        await _container.Open(NextMonday);

        _container.PickMeal(NextMonday.AddDays(2), _meals[5]);

        Assert.Same(_meals[5], _container.Days[2].Meal);
        Assert.Equal(_meals[3].Id, _container.Days[2].SavedMealId);
    }

    [Fact]
    public async Task ClearDay_EmptiesAndUnlocksTheDay()
    {
        await OpenAndGenerate();
        var date = NextMonday.AddDays(1);
        _container.ToggleLock(date);

        _container.ClearDay(date);

        Assert.Null(_container.Days[1].Meal);
        Assert.False(_container.Days[1].IsLocked);
    }

    [Fact]
    public async Task ClearDay_DayWithASavedMeal_ChangesNothing()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        await _container.Open(NextMonday);
        Assert.True(_container.HasSavedMeal(NextMonday.AddDays(2)));

        _container.ClearDay(NextMonday.AddDays(2));

        Assert.Same(_meals[3], _container.Days[2].Meal);
    }

    [Fact]
    public async Task Save_WritesAHandPickedLockedDay()
    {
        await _container.Open(NextMonday);
        _container.PickMeal(NextMonday.AddDays(1), _eatOut);
        SetupSave(Result.Ok().WithSuccess("Week saved"));

        await _container.Save();

        _mediator.Verify(m => m.Send(
            It.Is<SaveGeneratedWeek.Command>(c => c.Days.Count == 1 && c.Days[0].MealId == _eatOut.Id && c.Days[0].Date == NextMonday.AddDays(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Save_Success_ClearsHasPreview()
    {
        await OpenAndGenerate();
        SetupSave(Result.Ok().WithSuccess("Week saved"));

        await _container.Save();

        Assert.False(_container.HasPreview);
    }

    [Fact]
    public async Task EveryMutatingMethod_Notifies()
    {
        SetupWeek(PlanWith(NextMonday.AddDays(2), _meals[3]));
        SetupSave(Result.Ok().WithSuccess("Week saved"));
        await _container.Open(NextMonday);

        foreach (var action in new Func<Task>[]
        {
            () => _container.NavigateToWeek(NextMonday),
            () => _container.SetWeeksBack(5),
            () => _container.SetLeftoversAfterEveryMeal(true),
            () => { _container.ToggleLock(NextMonday.AddDays(2)); return Task.CompletedTask; },
            () => _container.Generate(),
            () => _container.Save(),
        })
        {
            _notifications = 0;
            await action();
            Assert.True(_notifications > 0);
        }
    }

    private static MealPlan PlanWith(DateTime date, Meal meal)
    {
        var plan = new MealPlan(date) { Id = 1 };
        plan.MealOnDate[date.Date] = meal;
        return plan;
    }

    private void SetupMeals(List<Meal> meals) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetAllMeals.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(meals));

    private void SetupWeek(MealPlan? plan) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetMealPlanByDate.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(plan));

    private void SetupPlannedDates(List<PlannedMealDate> dates) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetMealPlannedDates.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(dates));

    private void SetupSave(Result result) =>
        _mediator.Setup(m => m.Send(It.IsAny<SaveGeneratedWeek.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupSettings(PlanWeekSettings settings) =>
        _localStorage
            .Setup(s => s.GetItemAsync<PlanWeekSettings>(PlanWeekStateContainer.SettingsStorageKey, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<PlanWeekSettings?>(settings));

    private void VerifyToast(string message, Severity severity) =>
        _snackbar.Verify(s => s.Add(message, severity, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);

    /// <summary>Records where the container navigates, relative to the base uri.</summary>
    private sealed class FakeNavigationManager : NavigationManager
    {
        public Action<string>? OnNavigate { get; set; }

        public FakeNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            OnNavigate?.Invoke(ToBaseRelativePath(ToAbsoluteUri(uri).ToString()));
    }
}
