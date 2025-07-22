using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.Meals.State;

public interface IMealsStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    List<Meal> Meals { get; set; }

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadMeals();
    Task DeleteMeal(Meal meal);
}

public class MealsStateContainer(
    IMediator mediator) : IMealsStateContainer
{
    public void NotifyStateChanged() => OnStateChanged?.Invoke();

    public event Action OnStateChanged;

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

    public async Task DeleteMeal(Meal meal)
    {
        try
        {
            var result = await mediator.Send(new DeleteMeal.DeleteMeal.Command(meal.Id)); // Example MealId
            result.Handle(CurrentSnackbar);

            if (result.IsSuccess)
            {
                Meals.RemoveAll(m => m.Id == meal.Id);
                NotifyStateChanged();
            }
        }
        catch (Exception e)
        {

        }
    }
}