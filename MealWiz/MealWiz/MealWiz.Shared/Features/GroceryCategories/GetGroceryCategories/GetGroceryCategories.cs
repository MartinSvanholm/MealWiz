using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;

public static class GetGroceryCategories
{
    public record Query() : IRequest<Result<List<GroceryCategory>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<List<GroceryCategory>>>
    {
        public async Task<Result<List<GroceryCategory>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<GroceryCategoryDb>> () => await supabaseClient
                .From<GroceryCategoryDb>()
                .Order("position", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(cancellationToken));

            return result.Map(value => value.Models.ConvertAll(db => new GroceryCategory(db)));
        }
    }
}
