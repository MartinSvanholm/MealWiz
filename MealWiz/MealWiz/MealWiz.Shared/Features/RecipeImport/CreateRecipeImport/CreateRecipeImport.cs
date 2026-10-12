using FluentResults;
using MealWiz.Shared.Features.RecipeImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.RecipeImport.CreateRecipeImport;

public static class CreateRecipeImport
{
    /// <param name="Id">Client-generated, so the caller can subscribe to the row before the Function runs.</param>
    public record Command(Guid Id, string SourceUrl) : IRequest<Result<Models.RecipeImport>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Models.RecipeImport>>
    {
        public async Task<Result<Models.RecipeImport>> Handle(Command request, CancellationToken cancellationToken)
        {
            var recipeImportDb = new Models.RecipeImport
            {
                Id = request.Id,
                SourceUrl = request.SourceUrl,
                Status = RecipeImportStatus.Pending
            }.MapToRecipeImportDb();

            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            recipeImportDb.CreatedAt = DateTime.UtcNow;
            recipeImportDb.CreatedBy = new Guid(userId);

            var insertResult = await Result.Try(async Task<ModeledResponse<RecipeImportDb>> () => await supabaseClient
                .From<RecipeImportDb>()
                .Insert(recipeImportDb, cancellationToken: cancellationToken));

            return insertResult.Map(r => new Models.RecipeImport(r.Model ?? recipeImportDb));
        }
    }
}
