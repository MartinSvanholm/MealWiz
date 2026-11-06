using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;

/// <summary>
/// Keeps items the user chose not to remove from the review: deletes their sources and marks them manual, so they no
/// longer count as coming from a meal. Ids RLS hides are skipped.
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

            var deleted = await Result.Try(async Task () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Filter(x => x.GroceryItemId, Operator.In, ids)
                .Delete(cancellationToken: cancellationToken));
            if (deleted.IsFailed) return deleted;

            return await Result.Try(async Task () => await supabaseClient
                .From<GroceryItemDb>()
                .Filter(x => x.Id, Operator.In, ids)
                .Set(x => x.IsManual, true)
                .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                .Update(cancellationToken: cancellationToken));
        }
    }
}
