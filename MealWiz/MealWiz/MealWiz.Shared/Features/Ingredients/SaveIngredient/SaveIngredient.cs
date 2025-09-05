using FluentResults;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Meals.State;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Ingredients.SaveIngredient;

public static class SaveIngredient
{
    public record Command(Ingredient Ingredient) : IRequest<Result>;

    public class Handler(Client supabaseClient, IMealsStateContainer mealsStateContainer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var ingredientDb = request.Ingredient.MapToIngredientDb();

            if (request.Ingredient.Id == 0)
            {
                ingredientDb.CreatedAt = DateTime.UtcNow;
                ingredientDb.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id);
                ingredientDb.MealId = mealsStateContainer.MealToEdit.Id;

                return await Result.Try(async Task () => await supabaseClient
                    .From<IngredientDb>()
                    .Insert(ingredientDb));
            }
            else
            {
                ingredientDb.UpdatedAt = DateTime.UtcNow;

                return await Result.Try(async Task () => await supabaseClient
                    .From<IngredientDb>()
                    .Update(ingredientDb));
            }
        }
    }
}
