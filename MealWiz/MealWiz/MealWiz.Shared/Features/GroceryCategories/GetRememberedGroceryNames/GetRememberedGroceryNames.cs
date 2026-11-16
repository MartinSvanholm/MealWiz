using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.GroceryCategories.GetRememberedGroceryNames;

public static class GetRememberedGroceryNames
{
    public record Query() : IRequest<Result<List<RememberedGroceryName>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<List<RememberedGroceryName>>>
    {
        public async Task<Result<List<RememberedGroceryName>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<RememberedGroceryNameDb>> () => await supabaseClient
                .From<RememberedGroceryNameDb>()
                .Get(cancellationToken));

            return result.Map(value => value.Models.ConvertAll(db => new RememberedGroceryName(db)));
        }
    }
}
