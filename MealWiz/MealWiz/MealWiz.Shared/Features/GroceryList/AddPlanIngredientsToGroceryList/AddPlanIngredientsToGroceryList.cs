using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.AddPlanIngredientsToGroceryList;

/// <summary>
/// Puts planned ingredients on the grocery list and links each to its planned day. Ingredients already linked from that
/// planned day are skipped. Entries with an existing item update its amount; the rest become new items (one per name,
/// is_manual false). Every remaining entry gets a source. The amounts, the new items and the sources are written in one
/// transaction (the <c>add_plan_ingredients_to_grocery_list</c> database function): a failure changes nothing.
/// </summary>
public static class AddPlanIngredientsToGroceryList
{
    public record Command(List<PlanIngredientToAdd> Items) : IRequest<Result<List<GroceryItem>>>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryListResource> localizer) : IRequestHandler<Command, Result<List<GroceryItem>>>
    {
        public async Task<Result<List<GroceryItem>>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Items.Count == 0) return Result.Ok(new List<GroceryItem>());

            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);
            var user = new Guid(userId);

            if (request.Items.Any(item => item.PlannedMealId <= 0 || item.Ingredient.Id <= 0))
                return Result.Fail(localizer["GroceryItems_OnlyPlannedIngredients"]);

            var plannedMealIds = request.Items.Select(item => item.PlannedMealId).Distinct().ToList();
            var linkedResult = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDb>> () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Filter(x => x.PlannedMealId!, Operator.In, plannedMealIds)
                .Get(cancellationToken));
            if (linkedResult.IsFailed) return linkedResult.ToResult<List<GroceryItem>>();

            var linked = linkedResult.Value.Models.Select(source => (source.PlannedMealId, source.IngredientId)).ToHashSet();
            var entries = request.Items
                .Where(item => !linked.Contains((item.PlannedMealId, (int?)item.Ingredient.Id)))
                .ToList();
            if (entries.Count == 0) return Result.Ok(new List<GroceryItem>());

            // Existing items get the amount of their first entry; new items are one per name, with their first entry's
            // name and amount.
            var existingItems = entries
                .Where(e => e.ExistingGroceryItemId != null)
                .GroupBy(e => e.ExistingGroceryItemId!.Value)
                .Select(group => new { id = group.Key, amount = group.First().FinalAmount })
                .ToList();

            var newGroups = entries
                .Where(e => e.ExistingGroceryItemId == null)
                .GroupBy(e => e.Ingredient.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var newItems = newGroups
                .Select((group, key) => new { key, name = group.Key, amount = group.First().FinalAmount })
                .ToList();

            var newItemKeyOfEntry = new Dictionary<PlanIngredientToAdd, int>();
            for (var key = 0; key < newGroups.Count; key++)
                foreach (var entry in newGroups[key]) newItemKeyOfEntry[entry] = key;

            var sources = entries.ConvertAll(entry => new
            {
                item_id = entry.ExistingGroceryItemId,
                new_item_key = entry.ExistingGroceryItemId == null ? newItemKeyOfEntry[entry] : (int?)null,
                planned_meal_id = entry.PlannedMealId,
                ingredient_id = entry.Ingredient.Id,
                amount = entry.Ingredient.Amount ?? string.Empty
            });

            // One call, one transaction: the amounts, the new items and every link are written together or not at all.
            var written = await Result.Try(
                async Task<string?> () => (await supabaseClient.Rpc("add_plan_ingredients_to_grocery_list", new Dictionary<string, object>
                {
                    ["existing_items"] = existingItems,
                    ["new_items"] = newItems,
                    ["sources"] = sources
                })).Content,
                exception => exception.Message.Contains("grocery_item_not_found") ? new Error(localizer["GroceryItem_NotFound"]).CausedBy(exception)
                    : exception.Message.Contains("grocery_item_source_not_added") ? new Error(localizer["GroceryItems_NotAdded"]).CausedBy(exception)
                    : ResultHelper.MapException(exception));
            if (written.IsFailed) return written.ToResult<List<GroceryItem>>();

            var ids = Result.Try(() => JsonSerializer.Deserialize<WrittenIds>(written.Value ?? "null"));
            if (ids.IsFailed || ids.Value is null || ids.Value.Inserted.Count < newGroups.Count)
                return Result.Fail(localizer["GroceryItems_NotAdded"]);

            // Read the written items back: the updated ones first, then the new ones in the order they were added.
            var writtenIds = ids.Value.Updated.Concat(ids.Value.Inserted).ToList();
            var itemsResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                .From<GroceryItemDb>()
                .Filter(x => x.Id, Operator.In, writtenIds)
                .Get(cancellationToken));
            if (itemsResult.IsFailed) return itemsResult.ToResult<List<GroceryItem>>();

            var rowById = itemsResult.Value.Models.ToDictionary(row => row.Id);
            return Result.Ok(writtenIds.Where(rowById.ContainsKey).Select(id => new GroceryItem(rowById[id])).ToList());
        }

        private sealed record WrittenIds(
            [property: JsonPropertyName("updated")] List<int> Updated,
            [property: JsonPropertyName("inserted")] List<int> Inserted);
    }
}
