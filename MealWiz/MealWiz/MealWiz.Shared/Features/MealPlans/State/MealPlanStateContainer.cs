using Blazored.LocalStorage;
using AddMealToMealPlanAction = MealWiz.Shared.Features.MealPlans.AddMealToMealPlan.AddMealToMealPlan;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.MealPlans.State;

public interface IMealPlanStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    MealPlan? MealPlan { get; set; }
    DateTime SelectedDate { get; set; }
    DateTime SelectedWeekStart { get; }
    bool SelectedDateHasMeal { get; }
    bool IsLoadingWeek { get; }
    bool IsCurrentWeek();

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadMealPlan();
    Task NavigateToWeek(DateTime targetDate);
    Task AddMealToPlan(Meal meal);
    Meal? GetMealFromSelectedDate();
    bool DateHasMeal(DateTime dateTime);
}

public class MealPlanStateContainer(
    IMediator mediator,
    ILocalStorageService localStorage) : StateContainerBase, IMealPlanStateContainer
{
    private const string WeekStorageKey = "mealplan_selected_week";

    public MealPlan? MealPlan
    {
        get => mealPlan;
        set
        {
            mealPlan = value;
            NotifyStateChanged();
        }
    }
    private MealPlan? mealPlan { get; set; }

    public DateTime SelectedDate
    {
        get => selectedDate;
        set
        {
            selectedDate = value;
            NotifyStateChanged();
        }
    }
    private DateTime selectedDate { get; set; } = DateTime.Now;

    public DateTime SelectedWeekStart { get; private set; } = DateTime.Now.StartOfWeek().Date;

    public bool IsLoadingWeek { get; private set; } = true;

    public bool IsCurrentWeek() =>
        SelectedWeekStart == DateTime.Now.StartOfWeek().Date;

    public async Task LoadMealPlan()
    {
        var result = await mediator.Send(new GetMealPlanByDate.GetMealPlanByDate.Query(SelectedWeekStart));
        result.Handle(CurrentSnackbar);

        MealPlan = result.Value;
    }

    public async Task NavigateToWeek(DateTime targetDate)
    {
        SelectedWeekStart = targetDate.StartOfWeek().Date;
        SelectedDate = IsCurrentWeek() ? DateTime.Now.Date : SelectedWeekStart;
        IsLoadingWeek = true;
        NotifyStateChanged();

        await localStorage.SetItemAsync(WeekStorageKey, SelectedWeekStart);
        await LoadMealPlan();

        IsLoadingWeek = false;
        NotifyStateChanged();
    }

    public async Task AddMealToPlan(Meal meal)
    {
        meal.MealDate = SelectedDate.Date;
        meal.MealPlan = MealPlan;

        var result = await mediator.Send(new AddMealToMealPlanAction.Command(meal));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        MealPlan!.MealOnDate.TryAdd(result.Value.MealDate!.Value, result.Value);
        NotifyStateChanged();
    }

    public Meal? GetMealFromSelectedDate()
    {
        return GetMealFromDate(SelectedDate);
    }

    public bool DateHasMeal(DateTime dateTime)
    {
        return GetMealFromDate(dateTime.Date) != null;
    }

    private Meal? GetMealFromDate(DateTime dateTime)
    {
        Meal? meal = null;
        MealPlan?.MealOnDate.TryGetValue(dateTime.Date, out meal);

        return meal;
    }

    public bool SelectedDateHasMeal => GetMealFromSelectedDate() != null;
}
