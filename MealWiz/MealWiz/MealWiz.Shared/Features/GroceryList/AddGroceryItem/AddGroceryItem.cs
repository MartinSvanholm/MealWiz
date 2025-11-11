using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.Ingredients.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MealWiz.Shared.Features.GroceryList.AddGroceryItem;

public static class AddGroceryItem
{
    public record Command(GroceryItem GroceryItem) : IRequest<Result<int>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<int>>
    {
        public async Task<Result<int>> Handle(Command request, CancellationToken cancellationToken)
        {
            var groceryItemDb = request.GroceryItem.MapToDb();

            groceryItemDb.CreatedAt = DateTime.UtcNow;
            groceryItemDb.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id);

            var result = await Result.Try(async Task<ModeledResponse<GroceryItemDb>>() => await supabaseClient
                .From<GroceryItemDb>()
                .Insert(groceryItemDb));

            return result.Map(value => value.Model.Id);
        }
    }
}