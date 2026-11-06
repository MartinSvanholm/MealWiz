using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;

/// <summary>Every source of the user's grocery items, with the planned day's meal and date and the ingredient's meal and name. RLS limits it to the user's own.</summary>
public static class GetGroceryItemSources
{
    public record Query() : IRequest<Result<List<GroceryItemSource>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<List<GroceryItemSource>>>
    {
        public async Task<Result<List<GroceryItemSource>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDetailsDb>> () => await supabaseClient
                .From<GroceryItemSourceDetailsDb>()
                .Get(cancellationToken));

            return result.Map(value => value.Models.ConvertAll(db => new GroceryItemSource(db)));
        }
    }
}
