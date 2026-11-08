using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;

/// <summary>
/// Deletes the picked items in one request. It takes the ids the user saw in the basket, not "every picked row", so an
/// item ticked on another device in the meantime stays. Sources cascade.
/// </summary>
public static class DeleteGroceryItems
{
    public record Command(List<int> GroceryItemIds) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.GroceryItemIds.Count == 0) return Result.Ok();

            return await Result.Try(async Task () => await supabaseClient
                .From<GroceryItemDb>()
                .Filter(x => x.Id, Operator.In, request.GroceryItemIds)
                .Delete(cancellationToken: cancellationToken));
        }
    }
}
