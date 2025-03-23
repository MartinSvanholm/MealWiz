using MealWizFeatures.Helpers;
using MediatR;
using MudBlazor;

namespace MealWizFeatures.Features.Meals.State;

public interface IMealsStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    List<Meal> Meals { get; set; }

    event Action OnStateChange;

    void NotifyStateChanged();
    Task LoadMeals();
}

public class MealsStateContainer (
    IMediator mediator) : IMealsStateContainer
{
    public void NotifyStateChanged() => OnStateChange?.Invoke();

    public event Action OnStateChange;

    public ISnackbar CurrentSnackbar { get; set; }

    private List<Meal> meals { get; set; } = [];
    public List<Meal> Meals
    {
        get => meals;
        set
        {
            meals = value;
            NotifyStateChanged();
        }
    }

    public async Task LoadMeals()
    {
        var result = await mediator.Send(new GetAllMeals.GetAllMeals.Query());
        result.Handle(CurrentSnackbar);

        Meals = result.Value;
    }
}