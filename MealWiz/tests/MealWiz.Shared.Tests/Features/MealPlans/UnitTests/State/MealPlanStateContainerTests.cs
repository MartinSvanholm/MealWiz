using Blazored.LocalStorage;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;
using MealWiz.Shared.Features.MealPlans.CreateMealPlan;
using MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.MealPlans.State;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Moq;
using MudBlazor;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.UnitTests.State;

/// <summary>
/// The container reads <see cref="DateTime.Now"/> directly, so these tests use weeks in the past (never the current week)
/// unless they're about the current week.
/// </summary>
public class MealPlanStateContainerTests
{
    private static readonly DateTime PastWednesday = new(2026, 1, 7, 15, 30, 0);
    private static readonly DateTime PastMonday = new(2026, 1, 5);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILocalStorageService> _localStorage = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly MealPlanStateContainer _container;
    private int _notifications;

    public MealPlanStateContainerTests()
    {
        _container = new MealPlanStateContainer(_mediator.Object, _localStorage.Object) { CurrentSnackbar = _snackbar.Object };
        _container.OnStateChanged += () => _notifications++;
    }

    [Fact]
    public async Task NavigateToWeek_PastWeek_LoadsThatWeeksPlanAndSelectsItsMonday()
    {
        var plan = new MealPlan(PastMonday) { Id = 5 };
        SetupLoad(Result.Ok<MealPlan?>(plan));

        await _container.NavigateToWeek(PastWednesday);

        Assert.Equal(PastMonday, _container.SelectedWeekStart);
        Assert.Equal(PastMonday, _container.SelectedDate);
        Assert.False(_container.IsCurrentWeek());
        Assert.False(_container.IsLoadingWeek);
        Assert.Same(plan, _container.MealPlan);
        _mediator.Verify(m => m.Send(new GetMealPlanByDate.Query(PastMonday), It.IsAny<CancellationToken>()), Times.Once);
        _localStorage.Verify(s => s.SetItemAsync("mealplan_selected_week", PastMonday, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(_notifications > 0);
        VerifyNoToast();
    }

    [Fact]
    public async Task NavigateToWeek_WeekWithoutAPlan_ClearsThePlan()
    {
        _container.MealPlan = new MealPlan(DateTime.Now);
        SetupLoad(Result.Ok<MealPlan?>(null));

        await _container.NavigateToWeek(PastWednesday);

        Assert.Null(_container.MealPlan);
        Assert.False(_container.IsLoadingWeek);
    }

    [Fact]
    public async Task NavigateToWeek_CurrentWeek_SelectsToday()
    {
        SetupLoad(Result.Ok<MealPlan?>(null));

        await _container.NavigateToWeek(DateTime.Now);

        Assert.True(_container.IsCurrentWeek());
        Assert.Equal(DateTime.Now.StartOfWeek().Date, _container.SelectedWeekStart);
        Assert.Equal(DateTime.Now.Date, _container.SelectedDate);
    }

    [Fact]
    public async Task NavigateToWeek_LoadFails_ShowsAnErrorAndFinishesLoadingWithNoPlan()
    {
        _container.MealPlan = new MealPlan(DateTime.Now);
        SetupLoad(Result.Fail<MealPlan?>("Load failed"));

        await _container.NavigateToWeek(PastWednesday);

        Assert.Null(_container.MealPlan);
        Assert.False(_container.IsLoadingWeek);
        Assert.Equal(PastMonday, _container.SelectedWeekStart);
        VerifyToast("Load failed", Severity.Error);
    }

    [Fact]
    public async Task AddMealToPlan_WithoutAPlan_CreatesOneForTheSelectedWeekThenAddsTheMeal()
    {
        await NavigateToPastWeekWithoutPlan();
        _container.SelectedDate = PastWednesday;
        var created = new MealPlan(PastMonday) { Id = 5 };
        MealPlan? sentPlan = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateMealPlan.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateMealPlan.Command c, CancellationToken _) =>
            {
                sentPlan = c.MealPlan;
                return Result.Ok(created);
            });
        SetupAddEchoesTheMeal();
        var meal = new Meal { Id = 1, Name = "Pasta" };

        await _container.AddMealToPlan(meal);

        Assert.Equal(PastMonday, sentPlan?.StartDate);
        Assert.Equal(new DateTime(2026, 1, 11), sentPlan?.EndDate);
        Assert.Same(created, _container.MealPlan);
        Assert.Equal(PastWednesday.Date, meal.MealDate);
        Assert.Same(created, meal.MealPlan);
        Assert.Same(meal, created.MealOnDate[PastWednesday.Date]);
    }

    [Fact]
    public async Task AddMealToPlan_CreateFails_ShowsAnErrorAndDoesNotAddTheMeal()
    {
        await NavigateToPastWeekWithoutPlan();
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateMealPlan.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<MealPlan>("Create failed"));

        await _container.AddMealToPlan(new Meal { Id = 1 });

        Assert.Null(_container.MealPlan);
        _mediator.Verify(m => m.Send(It.IsAny<AddMealToMealPlan.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyToast("Create failed", Severity.Error);
    }

    [Fact]
    public async Task AddMealToPlan_ExistingPlan_AddsTheMealOnTheSelectedDayAndNotifies()
    {
        var plan = new MealPlan(PastMonday) { Id = 5 };
        _container.MealPlan = plan;
        _container.SelectedDate = PastWednesday;
        _notifications = 0;
        SetupAddEchoesTheMeal();
        var meal = new Meal { Id = 1, Name = "Pasta" };

        await _container.AddMealToPlan(meal);

        _mediator.Verify(m => m.Send(It.IsAny<CreateMealPlan.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Same(meal, plan.MealOnDate[PastWednesday.Date]);
        Assert.Same(plan, meal.MealPlan);
        Assert.True(_container.SelectedDateHasMeal);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task AddMealToPlan_AddFails_ShowsAnErrorAndLeavesThePlanUnchanged()
    {
        var plan = new MealPlan(PastMonday) { Id = 5 };
        _container.MealPlan = plan;
        _container.SelectedDate = PastWednesday;
        _notifications = 0;
        _mediator
            .Setup(m => m.Send(It.IsAny<AddMealToMealPlan.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<Meal>("Add failed"));

        await _container.AddMealToPlan(new Meal { Id = 1 });

        Assert.Empty(plan.MealOnDate);
        Assert.Equal(0, _notifications);
        VerifyToast("Add failed", Severity.Error);
    }

    [Fact]
    public void MealLookups_IgnoreTheTimeOfDay()
    {
        var meal = new Meal { Id = 1, Name = "Pasta" };
        var plan = new MealPlan(PastMonday);
        plan.MealOnDate[PastWednesday.Date] = meal;
        _container.MealPlan = plan;

        _container.SelectedDate = PastWednesday; // 15:30
        Assert.Same(meal, _container.GetMealFromSelectedDate());
        Assert.True(_container.SelectedDateHasMeal);
        Assert.True(_container.DateHasMeal(PastWednesday.AddHours(5)));

        _container.SelectedDate = PastMonday;
        Assert.Null(_container.GetMealFromSelectedDate());
        Assert.False(_container.SelectedDateHasMeal);
        Assert.False(_container.DateHasMeal(PastMonday));
    }

    [Fact]
    public void MealLookups_WithoutAPlan_FindNothing()
    {
        Assert.Null(_container.GetMealFromSelectedDate());
        Assert.False(_container.SelectedDateHasMeal);
        Assert.False(_container.DateHasMeal(PastWednesday));
    }

    [Fact]
    public void Setters_Notify()
    {
        _container.MealPlan = null;
        _container.SelectedDate = PastWednesday;

        Assert.Equal(2, _notifications);
    }

    private void SetupLoad(Result<MealPlan?> result) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<GetMealPlanByDate.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupAddEchoesTheMeal() =>
        _mediator
            .Setup(m => m.Send(It.IsAny<AddMealToMealPlan.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AddMealToMealPlan.Command c, CancellationToken _) => Result.Ok(c.Meal));

    private async Task NavigateToPastWeekWithoutPlan()
    {
        SetupLoad(Result.Ok<MealPlan?>(null));
        await _container.NavigateToWeek(PastWednesday);
        _notifications = 0;
    }

    private void VerifyToast(string message, Severity severity) =>
        _snackbar.Verify(s => s.Add(message, severity, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);

    private void VerifyNoToast() =>
        _snackbar.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);
}
