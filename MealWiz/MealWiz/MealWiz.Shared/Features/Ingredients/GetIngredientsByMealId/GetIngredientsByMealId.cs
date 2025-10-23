using FluentResults;
using MealWiz.Shared.Features.Ingredients.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;

public static class GetIngredientsByMealId
{
    public record Query(int MealId) : IRequest<Result<List<Ingredient>>>;

    public class Handler(Supabase.Client supabaseClient) : IRequestHandler<Query, Result<List<Ingredient>>>
    {
        public async Task<Result<List<Ingredient>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var supabaseResult = await Result.Try(() => supabaseClient
                .From<IngredientDb>()
                .Filter("fk_meal", Constants.Operator.Equals, request.MealId)
                .Get());

            if (supabaseResult.IsFailed)
            {
                return Result.Fail(supabaseResult.Errors);
            }

            var ingredients = supabaseResult.Value.Models.ConvertAll(i => new Ingredient(i));
            return Result.Ok(ingredients);
        }
    }
}