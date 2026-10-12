using FluentResults;
using MealWiz.Shared.Features.Ingredients.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Ingredients.SaveIngredient;

public static class SaveIngredient
{
    /// <param name="MealId">Meal a new ingredient is attached to (unused when updating an existing ingredient).</param>
    public record Command(Ingredient Ingredient, int MealId) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var ingredientDb = request.Ingredient.MapToIngredientDb();

            if (request.Ingredient.Id == 0)
            {
                ingredientDb.CreatedAt = DateTime.UtcNow;
                ingredientDb.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id);
                ingredientDb.MealId = request.MealId;

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
