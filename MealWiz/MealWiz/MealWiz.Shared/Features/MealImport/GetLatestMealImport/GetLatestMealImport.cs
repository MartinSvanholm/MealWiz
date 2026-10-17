using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealImport.GetLatestMealImport;

public static class GetLatestMealImport
{
    /// <summary>The current user's newest import (RLS scopes the table to the user), or null if they have none.</summary>
    public record Query : IRequest<Result<Models.MealImport?>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<Models.MealImport?>>
    {
        public async Task<Result<Models.MealImport?>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<MealImportDb>> () => await supabaseClient
                .From<MealImportDb>()
                .Order(r => r.CreatedAt, Ordering.Descending)
                .Limit(1)
                .Get(cancellationToken));

            return result.Map(r =>
            {
                var latest = r.Models.FirstOrDefault();
                return latest is null ? null : new Models.MealImport(latest);
            });
        }
    }
}
