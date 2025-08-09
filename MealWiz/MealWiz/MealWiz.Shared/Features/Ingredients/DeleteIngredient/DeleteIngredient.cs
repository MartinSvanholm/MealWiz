using FluentResults;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Ingredients.DeleteIngredient;

public static class DeleteIngredient
{
    public record Command(Ingredient Ingredient) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var ingredientDb = request.Ingredient.MapToIngredientDb();

            var supabaseResult = await Result.Try(() => supabaseClient.From<IngredientDb>().Delete(ingredientDb));
            return new Result().WithErrors(supabaseResult.Errors);
        }
    }
}