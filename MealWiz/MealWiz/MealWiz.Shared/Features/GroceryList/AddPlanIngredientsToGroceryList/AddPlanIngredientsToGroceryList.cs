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
/// is_manual false). Every remaining entry gets a source. The steps run in turn and are not atomic: a failure stops and
/// returns its error.
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

            var updated = new List<GroceryItem>();
            var itemIdOfEntry = new Dictionary<PlanIngredientToAdd, int>();

            foreach (var group in entries.Where(e => e.ExistingGroceryItemId != null).GroupBy(e => e.ExistingGroceryItemId!.Value))
            {
                var id = group.Key;
                var amount = group.First().FinalAmount;
                var updateResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                    .From<GroceryItemDb>()
                    .Where(x => x.Id == id)
                    .Set(x => x.Amount!, amount)
                    .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                    .Update(cancellationToken: cancellationToken));
                if (updateResult.IsFailed) return updateResult.ToResult<List<GroceryItem>>();
                if (updateResult.Value.Models.Count == 0) return Result.Fail(localizer["GroceryItem_NotFound"]);

                updated.Add(new GroceryItem(updateResult.Value.Models[0]));
                foreach (var entry in group) itemIdOfEntry[entry] = id;
            }

            var inserted = new List<GroceryItem>();
            var newGroups = entries
                .Where(e => e.ExistingGroceryItemId == null)
                .GroupBy(e => e.Ingredient.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (newGroups.Count > 0)
            {
                var newItems = newGroups.ConvertAll(group => new GroceryItem
                {
                    Name = group.Key,
                    Amount = group.First().FinalAmount,
                    IsPicked = false,
                    IsManual = false,
                    CreatedBy = user,
                    CreatedAt = DateTime.UtcNow
                });

                var insertResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                    .From<GroceryItemDb>()
                    .Insert(newItems.ConvertAll(item => item.MapToDb()), cancellationToken: cancellationToken));
                if (insertResult.IsFailed) return insertResult.ToResult<List<GroceryItem>>();
                if (insertResult.Value.Models.Count < newGroups.Count) return Result.Fail(localizer["GroceryItems_NotAdded"]);

                // Bulk insert order is not guaranteed: find each group's row by name.
                var rowByName = insertResult.Value.Models.ToDictionary(row => row.Name.Trim(), StringComparer.OrdinalIgnoreCase);
                foreach (var group in newGroups)
                {
                    var row = rowByName[group.Key];
                    inserted.Add(new GroceryItem(row));
                    foreach (var entry in group) itemIdOfEntry[entry] = row.Id;
                }
            }

            var sources = entries.ConvertAll(entry => new GroceryItemSource
            {
                GroceryItemId = itemIdOfEntry[entry],
                PlannedMealId = entry.PlannedMealId,
                IngredientId = entry.Ingredient.Id,
                Amount = entry.Ingredient.Amount ?? string.Empty,
                CreatedBy = user,
                CreatedAt = DateTime.UtcNow
            }.MapToGroceryItemSourceDb());

            var sourceResult = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDb>> () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Insert(sources, cancellationToken: cancellationToken));
            if (sourceResult.IsFailed) return sourceResult.ToResult<List<GroceryItem>>();

            return Result.Ok(updated.Concat(inserted).ToList());
        }
    }
}
