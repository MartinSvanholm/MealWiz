using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.MealPlans.State;

public interface IMealPlanStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    MealPlan MealPlan { get; set; }

    event Action OnStateChanged;

    Task LoadMealPlan();
    void NotifyStateChanged();
}

public class MealPlanStateContainer(
    IMediator mediator) : IMealPlanStateContainer
{
    public event Action OnStateChanged;
    public void NotifyStateChanged() => OnStateChanged?.Invoke();
    public ISnackbar CurrentSnackbar { get; set; }

    public MealPlan MealPlan
    {
        get => mealPlan;
        set
        {
            mealPlan = value;
            NotifyStateChanged();
        }
    }
    private MealPlan mealPlan { get; set; } = new();

    public async Task LoadMealPlan()
    {
        var result = await mediator.Send(new GetMealPlanByDate.GetMealPlanByDate.Query(DateTime.Now));
        result.Handle(CurrentSnackbar);

        MealPlan = result.Value;
    }
}