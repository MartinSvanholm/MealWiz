using FluentResults;
using MealWiz.Shared.Features.RecipeImport.Models;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.RecipeImport.GetLatestRecipeImport;

public static class GetLatestRecipeImport
{
    /// <summary>The current user's newest import (RLS scopes the table to the user), or null if they have none.</summary>
    public record Query : IRequest<Result<Models.RecipeImport?>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<Models.RecipeImport?>>
    {
        public async Task<Result<Models.RecipeImport?>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<RecipeImportDb>> () => await supabaseClient
                .From<RecipeImportDb>()
                .Order(r => r.CreatedAt, Ordering.Descending)
                .Limit(1)
                .Get(cancellationToken));

            return result.Map(r =>
            {
                var latest = r.Models.FirstOrDefault();
                return latest is null ? null : new Models.RecipeImport(latest);
            });
        }
    }
}
