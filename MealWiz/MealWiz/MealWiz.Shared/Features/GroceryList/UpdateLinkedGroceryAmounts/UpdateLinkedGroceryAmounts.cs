using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.UpdateLinkedGroceryAmounts;

/// <summary>
/// Brings grocery items up to date with ingredients whose amount changed after they were added. Checks that each item is
/// the user's and still to buy and that each source belongs to its item, then sets each item's amount once and each
/// source's remembered amount in one transaction (the <c>update_linked_grocery_amounts</c> database function): a
/// failure changes nothing.
/// </summary>
public static class UpdateLinkedGroceryAmounts
{
    public record Command(List<LinkedAmountUpdate> Updates) : IRequest<Result>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryListResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            string ItemNotFound() => localizer["LinkedAmounts_ItemNotFound"];
            string LinkNotFound() => localizer["LinkedAmounts_LinkNotFound"];

            if (request.Updates.Count == 0) return Result.Ok();

            if (supabaseClient.Auth.CurrentSession?.User?.Id is null) return Result.Fail(ErrorMessages.NotSignedIn);

            if (request.Updates.Any(u => u.SourceId <= 0 || u.GroceryItemId <= 0))
                return Result.Fail(localizer["LinkedAmounts_OnlyLinked"].Value);

            var itemIds = request.Updates.Select(u => u.GroceryItemId).Distinct().ToList();
            var itemsResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                .From<GroceryItemDb>()
                .Filter(x => x.Id, Operator.In, itemIds)
                .Get(cancellationToken));
            if (itemsResult.IsFailed) return itemsResult.ToResult();

            var items = itemsResult.Value.Models;
            if (itemIds.Any(id => items.All(item => item.Id != id))) return Result.Fail(ItemNotFound());
            if (items.Any(item => item.IsPicked)) return Result.Fail(localizer["LinkedAmounts_InBasket"].Value);

            var sourceIds = request.Updates.Select(u => u.SourceId).Distinct().ToList();
            var sourcesResult = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDb>> () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Filter(x => x.Id, Operator.In, sourceIds)
                .Get(cancellationToken));
            if (sourcesResult.IsFailed) return sourcesResult.ToResult();

            var sources = sourcesResult.Value.Models;
            if (request.Updates.Any(u => !sources.Any(s => s.Id == u.SourceId && s.GroceryItemId == u.GroceryItemId)))
                return Result.Fail(LinkNotFound());

            // One call, one transaction: each item's amount once, then each link's remembered amount.
            var itemAmounts = request.Updates
                .GroupBy(u => u.GroceryItemId)
                .Select(group => new { id = group.Key, amount = group.First().FinalAmount })
                .ToList();
            var sourceAmounts = request.Updates
                .GroupBy(u => u.SourceId)
                .Select(group => new { id = group.Key, amount = group.Last().SourceAmount })
                .ToList();

            var written = await Result.Try(
                async Task () => await supabaseClient.Rpc("update_linked_grocery_amounts", new Dictionary<string, object>
                {
                    ["item_amounts"] = itemAmounts,
                    ["source_amounts"] = sourceAmounts
                }),
                exception => exception.Message.Contains("grocery_item_source_not_found") ? new Error(LinkNotFound()).CausedBy(exception)
                    : exception.Message.Contains("grocery_item_not_found") ? new Error(ItemNotFound()).CausedBy(exception)
                    : ResultHelper.MapException(exception));
            if (written.IsFailed) return written;

            return Result.Ok();
        }
    }
}
