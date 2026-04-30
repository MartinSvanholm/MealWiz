using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.GroceryList.AddMealPlanIngredientsToGroceryList;

public static class AddMealPlanIngredientsToGroceryList
{
    public record Command(List<GroceryItem> GroceryItems) : IRequest<Result<List<int>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<List<int>>>
    {
        public async Task<Result<List<int>>> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<GroceryItemDb>>() =>
                await supabaseClient
                    .From<GroceryItemDb>()
                    .Insert(request.GroceryItems.ConvertAll(item => item.MapToDb()))
            );

            return result.Map(value => value.Models.Select(item => item.Id).ToList());
        }
    }
}