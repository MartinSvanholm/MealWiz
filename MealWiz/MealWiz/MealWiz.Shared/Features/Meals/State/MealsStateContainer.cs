using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using FluentResults;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.Meals.State;

public interface IMealsStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    List<Meal> Meals { get; set; }
    Meal MealToEdit { get; set; }
    bool IsEdit { get; }

    event Action OnStateChanged;

    void NotifyStateChanged();
    void AddOrUpdatePendingIngredient(Ingredient ingredient);
    void RemovePendingIngredient(Ingredient ingredient);
    Task LoadMeals();
    Task ReloadIngredientsForMealToEdit();

    /// <summary>
    /// Adds or updates an ingredient of <see cref="MealToEdit"/>: kept pending while the meal is new, otherwise saved
    /// and the ingredients reloaded. The caller reports a failure (<c>Handle</c>).
    /// </summary>
    Task<Result> SaveIngredientForMealToEdit(Ingredient ingredient);
}

public class MealsStateContainer(
    IMediator mediator) : StateContainerBase, IMealsStateContainer
{
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

    public bool IsEdit => MealToEdit.Id > 0;

    public async Task LoadMeals()
    {
        var result = await mediator.Send(new GetAllMeals.GetAllMeals.Query());
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        Meals = result.Value;
    }

    public void AddOrUpdatePendingIngredient(Ingredient ingredient)
    {
        if (!MealToEdit.Ingredients.Contains(ingredient))
            MealToEdit.Ingredients.Add(ingredient);
        NotifyStateChanged();
    }

    public void RemovePendingIngredient(Ingredient ingredient)
    {
        MealToEdit.Ingredients.Remove(ingredient);
        NotifyStateChanged();
    }

    public async Task ReloadIngredientsForMealToEdit()
    {
        var result = await mediator.Send(new GetIngredientsByMealId.Query(MealToEdit.Id));

        result.Handle(CurrentSnackbar);
        if (result.IsFailed) return;

        MealToEdit.Ingredients = result.Value;
        NotifyStateChanged();
    }

    public async Task<Result> SaveIngredientForMealToEdit(Ingredient ingredient)
    {
        if (!IsEdit)
        {
            AddOrUpdatePendingIngredient(ingredient);
            return Result.Ok();
        }

        var result = await mediator.Send(new SaveIngredient.Command(ingredient, MealToEdit.Id));
        if (result.IsFailed) return result;

        await ReloadIngredientsForMealToEdit();
        return Result.Ok();
    }
}
