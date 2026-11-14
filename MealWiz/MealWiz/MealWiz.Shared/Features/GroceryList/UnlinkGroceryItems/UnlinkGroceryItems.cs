using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;

/// <summary>
/// Keeps items the user chose not to remove from the review: deletes their sources and marks them manual, so they no
/// longer count as coming from a meal. Both happen in one transaction (the <c>unlink_grocery_items</c> database
/// function). Ids RLS hides are skipped.
/// </summary>
public static class UnlinkGroceryItems
{
    public record Command(List<int> GroceryItemIds) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var ids = request.GroceryItemIds;
            if (ids.Count == 0) return Result.Ok();

            // One call, one transaction: the links go and the items become manual together.
            return await Result.Try(async Task () => await supabaseClient.Rpc(
                "unlink_grocery_items", new Dictionary<string, object> { ["item_ids"] = ids }));
        }
    }
}
