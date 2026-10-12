using FluentResults;
using MealWiz.Shared.Features.RecipeImport.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.RecipeImport.GetRecipeImport;

public static class GetRecipeImport
{
    public record Query(Guid Id) : IRequest<Result<Models.RecipeImport>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<Models.RecipeImport>>
    {
        public async Task<Result<Models.RecipeImport>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<RecipeImportDb?> () => await supabaseClient
                .From<RecipeImportDb>()
                .Where(r => r.Id == request.Id)
                .Single(cancellationToken));

            if (result.IsFailed) return result.ToResult<Models.RecipeImport>();
            if (result.Value is null) return Result.Fail("The recipe import could not be found.");

            return new Models.RecipeImport(result.Value);
        }
    }
}
