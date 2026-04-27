using Blazored.LocalStorage;
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
    Meal? GetMealFromSelectedDate();
}

public class MealPlanStateContainer(
    IMediator mediator,
    ILocalStorageService localStorage) : IMealPlanStateContainer
{
    private const string WeekStorageKey = "mealplan_selected_week";

    public event Action OnStateChanged;
    public void NotifyStateChanged() => OnStateChanged?.Invoke();
    public ISnackbar CurrentSnackbar { get; set; }

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

    public Meal? GetMealFromSelectedDate()
    {
        Meal? meal = null;
        MealPlan?.MealOnDate.TryGetValue(SelectedDate.Date, out meal);

        return meal;
    }

    public bool SelectedDateHasMeal => GetMealFromSelectedDate() != null;
}
