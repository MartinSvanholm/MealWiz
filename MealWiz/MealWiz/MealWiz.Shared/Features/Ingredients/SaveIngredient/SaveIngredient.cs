using FluentResults;
using MealWiz.Shared.Features.Meals.State;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Ingredients.SaveIngredient;

public static class SaveIngredient
{
    public record Command(Ingredient Ingredient) : IRequest<Result<Ingredient>>;

    public class Handler(Client supabaseClient, IMealsStateContainer mealsStateContainer) : IRequestHandler<Command, Result<Ingredient>>
    {
        public async Task<Result<Ingredient>> Handle(Command request, CancellationToken cancellationToken)
        {
            var ingredientDb = request.Ingredient.MapToIngredientDb();

            if (request.Ingredient.Id == 0)
            {
                ingredientDb.CreatedAt = DateTime.UtcNow;
                ingredientDb.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id);
                ingredientDb.MealId = mealsStateContainer.MealToEdit.Id;

                var supabaseResult = await Result.Try(() => supabaseClient.From<IngredientDb>().Insert(ingredientDb));
                return supabaseResult.Map(result => new Ingredient(supabaseResult.Value.Model));
            }
            else
            {
                ingredientDb.UpdatedAt = DateTime.UtcNow;

                var supabaseResult = await Result.Try(() => supabaseClient.From<IngredientDb>().Update(ingredientDb));
                return supabaseResult.Map(result => new Ingredient(supabaseResult.Value.Model));
            }
        }
    }
}
