using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Supabase.Postgrest;
using Supabase.Postgrest.Responses;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.GroceryCategories.RememberGroceryName;

public static class RememberGroceryName
{
    /// <summary>Puts <paramref name="Name"/> (normalised here) in a category, for every item and ingredient with that name.</summary>
    public record Command(string Name, int CategoryId) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var name = CategoryResolver.Normalise(request.Name);
            if (name.Length == 0) return Result.Ok();

            var row = new NewRememberedGroceryNameDb
            {
                Name = name,
                CategoryId = request.CategoryId,
                CreatedBy = new Guid(userId),
                UpdatedAt = DateTime.UtcNow,
            };

            // A name remembered before is moved: the upsert updates its row.
            var result = await Result.Try(async Task<ModeledResponse<NewRememberedGroceryNameDb>> () => await supabaseClient
                .From<NewRememberedGroceryNameDb>()
                .Upsert(row, new QueryOptions
                {
                    OnConflict = "created_by,name",
                    DuplicateResolution = QueryOptions.DuplicateResolutionType.MergeDuplicates,
                    Returning = QueryOptions.ReturnType.Minimal,
                }, cancellationToken));

            return result.ToResult();
        }
    }
}
