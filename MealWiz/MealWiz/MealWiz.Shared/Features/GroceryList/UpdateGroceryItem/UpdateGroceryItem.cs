using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;

public static class UpdateGroceryItem
{
    public record Command(GroceryItem GroceryItem) : IRequest<Result<int>>;

    public class Hanlder(Client supabaseClient) : IRequestHandler<Command, Result<int>>
    {
        public async Task<Result<int>> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                .From<GroceryItemDb>()
                .Update(request.GroceryItem.MapToDb()));

            return result.Map(value => value.Model.Id);
        }
    }
}