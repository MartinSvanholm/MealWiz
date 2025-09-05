using MealWiz.Shared.Features.Ingredients.EditIngredient;
using MealWiz.Shared.Services.DrawerStateContainer;
using MediatR;

namespace MealWiz.Shared.Features.Ingredients;

public interface IIngredientStateContainer
{
    Ingredient IngredientToEdit { get; set; }
    void EditIngredient(Ingredient ingredient);
}

public class IngredientStateContainer(IDrawerStateContainer _drawerStateContainer, IMediator mediator) : IIngredientStateContainer
{
    public Ingredient IngredientToEdit { get; set; }

    public void EditIngredient(Ingredient ingredient)
    {
        IngredientToEdit = ingredient;

        DrawerStateParameters drawerStateParameters = new DrawerStateParameters
        {
            Content = typeof(EditIngredientDialog),
            Title = "Edit Ingredient",
            Height = "38%"
        };

        _drawerStateContainer.OpenDrawer(drawerStateParameters);
    }
}