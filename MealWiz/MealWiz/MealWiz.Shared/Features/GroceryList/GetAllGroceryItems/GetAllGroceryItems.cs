using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;

public static class GetAllGroceryItems
{
    public record Query() : IRequest<Result<List<GroceryItem>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<List<GroceryItem>>>
    {
        public async Task<Result<List<GroceryItem>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient.From<GroceryItemDb>().Get());

            return result.Map(value => value.Models.ConvertAll(db => new GroceryItem(db)));
        }
    }
}
