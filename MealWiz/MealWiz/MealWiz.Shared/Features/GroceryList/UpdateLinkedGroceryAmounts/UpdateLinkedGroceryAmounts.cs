using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.UpdateLinkedGroceryAmounts;

/// <summary>
/// Brings grocery items up to date with ingredients whose amount changed after they were added. Checks that each item is
/// the user's and still to buy and that each source belongs to its item, sets each item's amount once, then each
/// source's remembered amount. The steps run in turn and are not atomic: a failure stops and returns its error.
/// </summary>
public static class UpdateLinkedGroceryAmounts
{
    private const string ItemNotFound = "The grocery item could not be found.";
    private const string LinkNotFound = "The grocery item's link could not be found.";

    public record Command(List<LinkedAmountUpdate> Updates) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Updates.Count == 0) return Result.Ok();

            if (supabaseClient.Auth.CurrentSession?.User?.Id is null) return Result.Fail(ErrorMessages.NotSignedIn);

            if (request.Updates.Any(u => u.SourceId <= 0 || u.GroceryItemId <= 0))
                return Result.Fail("Only linked grocery items can be updated.");

            var itemIds = request.Updates.Select(u => u.GroceryItemId).Distinct().ToList();
            var itemsResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                .From<GroceryItemDb>()
                .Filter(x => x.Id, Operator.In, itemIds)
                .Get(cancellationToken));
            if (itemsResult.IsFailed) return itemsResult.ToResult();

            var items = itemsResult.Value.Models;
            if (itemIds.Any(id => items.All(item => item.Id != id))) return Result.Fail(ItemNotFound);
            if (items.Any(item => item.IsPicked)) return Result.Fail("The grocery item is already in the basket.");

            var sourceIds = request.Updates.Select(u => u.SourceId).Distinct().ToList();
            var sourcesResult = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDb>> () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Filter(x => x.Id, Operator.In, sourceIds)
                .Get(cancellationToken));
            if (sourcesResult.IsFailed) return sourcesResult.ToResult();

            var sources = sourcesResult.Value.Models;
            if (request.Updates.Any(u => !sources.Any(s => s.Id == u.SourceId && s.GroceryItemId == u.GroceryItemId)))
                return Result.Fail(LinkNotFound);

            foreach (var group in request.Updates.GroupBy(u => u.GroceryItemId))
            {
                var id = group.Key;
                var amount = group.First().FinalAmount;
                var updateResult = await Result.Try(async Task<ModeledResponse<GroceryItemDb>> () => await supabaseClient
                    .From<GroceryItemDb>()
                    .Where(x => x.Id == id)
                    .Where(x => x.IsPicked == false)
                    .Set(x => x.Amount!, amount)
                    .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                    .Update(cancellationToken: cancellationToken));
                if (updateResult.IsFailed) return updateResult.ToResult();
                if (updateResult.Value.Models.Count == 0) return Result.Fail(ItemNotFound);
            }

            foreach (var update in request.Updates)
            {
                var sourceId = update.SourceId;
                var sourceAmount = update.SourceAmount;
                var updateResult = await Result.Try(async Task<ModeledResponse<GroceryItemSourceDb>> () => await supabaseClient
                    .From<GroceryItemSourceDb>()
                    .Where(x => x.Id == sourceId)
                    .Set(x => x.Amount, sourceAmount)
                    .Update(cancellationToken: cancellationToken));
                if (updateResult.IsFailed) return updateResult.ToResult();
                if (updateResult.Value.Models.Count == 0) return Result.Fail(LinkNotFound);
            }

            return Result.Ok();
        }
    }
}
