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
    bool SelectedDateHasMeal { get; }

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadMealPlan();
    Meal? GetMealFromSelectedDate();
}

public class MealPlanStateContainer(
    IMediator mediator) : IMealPlanStateContainer
{
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

    public async Task LoadMealPlan()
    {
        var result = await mediator.Send(new GetMealPlanByDate.GetMealPlanByDate.Query(DateTime.Now));
        result.Handle(CurrentSnackbar);

        MealPlan = result.Value;
    }

    public Meal? GetMealFromSelectedDate()
    {
        Meal? meal = null;
        MealPlan?.MealOnDate.TryGetValue(SelectedDate.Date, out meal);

        return meal;
    }

    public bool SelectedDateHasMeal => GetMealFromSelectedDate() != null;
}