using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Supabase;
using Supabase.Postgrest;
using Supabase.Postgrest.Responses;
using static Supabase.Postgrest.Constants;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;

public static class CreateDefaultGroceryCategories
{
    public record Command() : IRequest<Result<List<GroceryCategory>>>;

    /// <summary>
    /// Inserts the eleven default categories for the signed-in user, skipping any default the user already has (so two
    /// devices doing it at once leave one set), then returns all the user's categories by position.
    /// </summary>
    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<List<GroceryCategory>>>
    {
        public async Task<Result<List<GroceryCategory>>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var createdBy = new Guid(userId);
            var now = DateTime.UtcNow;
            var rows = DefaultCategoryKeyExtensions.All
                .Select(key => new NewGroceryCategoryDb
                {
                    DefaultKey = key.ToDbValue(),
                    Position = key.DefaultPosition(),
                    CreatedBy = createdBy,
                    CreatedAt = now,
                })
                .ToList();

            var insert = await Result.Try(async Task<ModeledResponse<NewGroceryCategoryDb>> () => await supabaseClient
                .From<NewGroceryCategoryDb>()
                .Upsert(rows, new QueryOptions
                {
                    OnConflict = "created_by,default_key",
                    DuplicateResolution = QueryOptions.DuplicateResolutionType.IgnoreDuplicates,
                    Returning = QueryOptions.ReturnType.Minimal,
                }, cancellationToken));
            if (insert.IsFailed) return insert.ToResult<List<GroceryCategory>>();

            var read = await Result.Try(async Task<ModeledResponse<GroceryCategoryDb>> () => await supabaseClient
                .From<GroceryCategoryDb>()
                .Order("position", Ordering.Ascending)
                .Order("id", Ordering.Ascending)
                .Get(cancellationToken));

            return read.Map(value => value.Models.ConvertAll(db => new GroceryCategory(db)));
        }
    }
}
