using FluentResults;
using MealWiz.Shared.Features.Ingredients.GetIngredientsById;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.Meals.State;

public interface IMealsStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    List<Meal> Meals { get; set; }
    Meal MealToEdit { get; set; }

    event Action OnStateChanged;

    void NotifyStateChanged();
    Task LoadMeals();
    Task ReloadIngredientsForMealToEdit();
}

public class MealsStateContainer(
    IMediator mediator) : IMealsStateContainer
{
    public event Action OnStateChanged;
    public void NotifyStateChanged() => OnStateChanged?.Invoke();

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

    public Meal MealToEdit { get; set; } = new Meal();

    public async Task LoadMeals()
    {
        var result = await mediator.Send(new GetAllMeals.GetAllMeals.Query());
        result.Handle(CurrentSnackbar);

        Meals = result.Value;
    }

    public async Task ReloadIngredientsForMealToEdit()
    {
        try
        {
            var result = await mediator.Send(new GetIngredientsByMealId.Query(MealToEdit.Id));

            result.Handle(CurrentSnackbar);
            if (result.IsFailed) return;

            MealToEdit.Ingredients = result.Value;
            NotifyStateChanged();
        }
        catch (Exception)
        {
            CurrentSnackbar.Add("An error orcured, please try again.", Severity.Error);
        }
    }
}