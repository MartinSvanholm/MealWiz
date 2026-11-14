using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;

/// <summary>
/// Drops the given grocery item sources and lowers each to-buy item's amount by theirs (MEA-93, MEA-99): a deleted or
/// removed meal's links on items another planned meal or the user still needs. Ids RLS hides are skipped.
/// The links are deleted and the amounts lowered in one transaction (the <c>drop_grocery_item_sources</c> database
/// function), so a failure changes nothing. Returns the items whose amount changed.
/// </summary>
public static class DeleteGroceryItemSources
{
    public record Command(List<int> SourceIds) : IRequest<Result<List<GroceryItem>>>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryListResource> localizer)
        : IRequestHandler<Command, Result<List<GroceryItem>>>
    {
        public async Task<Result<List<GroceryItem>>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.SourceIds.Count == 0) return Result.Ok(new List<GroceryItem>());

            var sourcesResult = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDb>> () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Filter(x => x.Id, Operator.In, request.SourceIds)
                .Get(cancellationToken));
            if (sourcesResult.IsFailed) return sourcesResult.ToResult<List<GroceryItem>>();

            var sources = sourcesResult.Value.Models.OrderBy(s => s.Id).ToList();
            if (sources.Count == 0) return Result.Ok(new List<GroceryItem>());

            var itemIds = sources.Select(s => s.GroceryItemId).Distinct().ToList();
            var itemsResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                .From<GroceryItemDb>()
                .Filter(x => x.Id, Operator.In, itemIds)
                .Get(cancellationToken));
            if (itemsResult.IsFailed) return itemsResult.ToResult<List<GroceryItem>>();

            // Basket items keep their amount; manual items are lowered too (a plan amount was merged into them).
            var changes = new List<(int Id, string Amount)>();
            foreach (var item in itemsResult.Value.Models.Where(i => !i.IsPicked).OrderBy(i => i.Id))
            {
                var amount = item.Amount;
                foreach (var source in sources.Where(s => s.GroceryItemId == item.Id))
                    amount = AmountHelper.Subtract(amount, source.Amount) ?? amount;

                if (amount is not null && amount != item.Amount) changes.Add((item.Id, amount));
            }

            // One call, one transaction: the links go and the amounts are lowered together.
            var written = await Result.Try(
                async Task () => await supabaseClient.Rpc("drop_grocery_item_sources", new Dictionary<string, object>
                {
                    ["source_ids"] = sources.Select(s => s.Id).ToList(),
                    ["item_amounts"] = changes.Select(change => new { id = change.Id, amount = change.Amount }).ToList()
                }),
                exception => exception.Message.Contains("grocery_item_not_found")
                    ? new Error(localizer["LinkedAmounts_ItemNotFound"]).CausedBy(exception)
                    : ResultHelper.MapException(exception));
            if (written.IsFailed) return written.ToResult<List<GroceryItem>>();

            var updated = new List<GroceryItem>();
            if (changes.Count > 0)
            {
                var changedIds = changes.Select(change => change.Id).ToList();
                var changedResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                    .From<GroceryItemDb>()
                    .Filter(x => x.Id, Operator.In, changedIds)
                    .Get(cancellationToken));
                if (changedResult.IsFailed) return changedResult.ToResult<List<GroceryItem>>();

                updated = changedResult.Value.Models.OrderBy(item => item.Id).Select(item => new GroceryItem(item)).ToList();
            }

            return Result.Ok(updated);
        }
    }
}
