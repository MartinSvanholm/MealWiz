using FluentResults;
using MealWiz.Shared.Features.RecipeImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.RecipeImport.MarkRecipeImportFailed;

/// <summary>
/// Client-side fallback for when the Function couldn't be reached, so the row doesn't stay pending forever.
/// Only touches the row while it's still pending (enforced by RLS too), so it never overwrites a result the
/// Function already wrote.
/// </summary>
public static class MarkRecipeImportFailed
{
    /// <returns>The updated row, or null when it was no longer pending (the Function already resolved it).</returns>
    public record Command(Guid Id, string ErrorMessage) : IRequest<Result<Models.RecipeImport?>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Models.RecipeImport?>>
    {
        public async Task<Result<Models.RecipeImport?>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var updateResult = await Result.Try(async Task<ModeledResponse<RecipeImportDb>> () => await supabaseClient
                .From<RecipeImportDb>()
                .Where(r => r.Id == request.Id)
                .Filter(r => r.Status, Operator.Equals, RecipeImportStatusExtensions.PendingValue)
                .Set(r => r.Status, RecipeImportStatusExtensions.FailedValue)
                .Set(r => r.ErrorMessage!, request.ErrorMessage)
                .Set(r => r.UpdatedAt!, DateTime.UtcNow)
                .Set(r => r.UpdatedBy!, new Guid(userId))
                .Update(cancellationToken: cancellationToken));

            return updateResult.Map(r =>
            {
                var updated = r.Models.FirstOrDefault();
                return updated is null ? null : new Models.RecipeImport(updated);
            });
        }
    }
}
